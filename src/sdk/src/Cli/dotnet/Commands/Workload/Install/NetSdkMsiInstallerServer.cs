﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.DotNet.Cli.Installer.Windows;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

[SupportedOSPlatform("windows")]
internal class NetSdkMsiInstallerServer : MsiInstallerBase
{
    private bool _done;
    private bool _shutdownRequested;

    public NetSdkMsiInstallerServer(InstallElevationContextBase elevationContext, PipeStreamSetupLogger logger, bool verifySignatures)
        : base(elevationContext, logger, verifySignatures)
    {
        // Establish a connection with the install client and logger. We're relying on tasks to handle
        // this, otherwise, the ordering needs to be lined up with how the client configures
        // the underlying pipe streams to avoid deadlock.
        Task dispatchTask = new(() => Dispatcher.Connect());
        Task loggerTask = new(() => logger.Connect());

        dispatchTask.Start();
        loggerTask.Start();

        Task.WaitAll(dispatchTask, loggerTask);
    }

    /// <summary>
    /// Starts the execution loop of the server.
    /// </summary>
    public void Run()
    {
        // Turn off automatic updates to reduce the risk of running into ERROR_INSTALL_ALREADY_RUNNING. We
        // also don't want MU to potentially patch the SDK while performing workload installations.
        UpdateAgent.Stop();

        while (!_done)
        {
            if (!Dispatcher.IsConnected || !IsParentProcessRunning)
            {
                _done = true;
                break;
            }

            InstallRequestMessage request = Dispatcher.ReceiveRequest();

            try
            {
                switch (request.RequestType)
                {
                    case InstallRequestType.Shutdown:
                        _shutdownRequested = true;
                        _done = true;
                        break;

                    case InstallRequestType.CachePayload:
                        Cache.CachePayload(request.PackageId, request.PackageVersion, request.ManifestPath);
                        Dispatcher.ReplySuccess($"Package Cached");
                        break;

                    case InstallRequestType.WriteWorkloadInstallationRecord:
                        RecordRepository.WriteWorkloadInstallationRecord(new WorkloadId(request.WorkloadId), new SdkFeatureBand(request.SdkFeatureBand));
                        Dispatcher.ReplySuccess($"Workload record created.");
                        break;

                    case InstallRequestType.DeleteWorkloadInstallationRecord:
                        RecordRepository.DeleteWorkloadInstallationRecord(new WorkloadId(request.WorkloadId), new SdkFeatureBand(request.SdkFeatureBand));
                        Dispatcher.ReplySuccess($"Workload record deleted.");
                        break;

                    case InstallRequestType.InstallMsi:
                        Dispatcher.Reply(InstallMsi(request.PackagePath, request.LogFile));
                        break;

                    case InstallRequestType.UninstallMsi:
                        Dispatcher.Reply(UninstallMsi(request.ProductCode, request.LogFile));
                        break;

                    case InstallRequestType.RepairMsi:
                        Dispatcher.Reply(RepairMsi(request.ProductCode, request.LogFile));
                        break;

                    case InstallRequestType.AddDependent:
                    case InstallRequestType.RemoveDependent:
                        UpdateDependent(request.RequestType, request.ProviderKeyName, request.Dependent);
                        Dispatcher.ReplySuccess($"Updated dependent '{request.Dependent}' for provider key '{request.ProviderKeyName}'");
                        break;

                    case InstallRequestType.SaveInstallStateManifestVersions:
                        SaveInstallStateManifestVersions(new SdkFeatureBand(request.SdkFeatureBand), request.InstallStateManifestVersions);
                        Dispatcher.ReplySuccess($"Created install state file for {request.SdkFeatureBand}.");
                        break;

                    case InstallRequestType.RemoveManifestsFromInstallStateFile:
                        RemoveManifestsFromInstallState(new SdkFeatureBand(request.SdkFeatureBand));
                        Dispatcher.ReplySuccess($"Deleted install state file for {request.SdkFeatureBand}.");
                        break;

                    case InstallRequestType.AdjustWorkloadMode:
                        UpdateInstallMode(new SdkFeatureBand(request.SdkFeatureBand), request.UseWorkloadSets);
                        string newMode = request.UseWorkloadSets == null ? "<null>" : request.UseWorkloadSets.Value ? "workload sets" : "loose manifests";
                        Dispatcher.ReplySuccess($"Updated install mode to use {newMode}.");
                        break;

                    case InstallRequestType.AdjustWorkloadSetVersion:
                        AdjustWorkloadSetInInstallState(new SdkFeatureBand(request.SdkFeatureBand), request.WorkloadSetVersion);
                        Dispatcher.ReplySuccess($"Updated workload set version in install state to {request.WorkloadSetVersion}.");
                        break;

                    case InstallRequestType.RecordWorkloadSetInGlobalJson:
                        RecordWorkloadSetInGlobalJson(new SdkFeatureBand(request.SdkFeatureBand), request.GlobalJsonPath, request.WorkloadSetVersion);
                        Dispatcher.ReplySuccess($"Recorded workload set {request.WorkloadSetVersion} in {request.GlobalJsonPath} for SDK feature band {request.SdkFeatureBand}.");
                        break;

                    case InstallRequestType.GetGlobalJsonWorkloadSetVersions:
                        Dispatcher.Reply(new InstallResponseMessage()
                        {
                            Message = "Got global.json GC roots",
                            HResult = Win32.Msi.Error.S_OK,
                            Error = Win32.Msi.Error.SUCCESS,
                            GlobalJsonWorkloadSetVersions = GetGlobalJsonWorkloadSetVersions(new SdkFeatureBand(request.SdkFeatureBand))
                        });
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown message request: {(int)request.RequestType}");
                }
            }
            catch (Exception e)
            {
                LogException(e);
                Dispatcher.Reply(e);
            }
        }
    }

    public void Shutdown()
    {
        // Restart the update agent if we shut it down.
        UpdateAgent.Start();

        Log?.LogMessage("Shutting down server.");

        if (_shutdownRequested)
        {
            Dispatcher.Reply(new InstallResponseMessage());
        }
    }

    /// <summary>
    /// Creates a new <see cref="NetSdkMsiInstallerServer"/> instance.
    /// </summary>
    /// <returns>A new install server.</returns>
    public static NetSdkMsiInstallerServer Create(bool verifySignatures)
    {
        if (!WindowsUtils.IsAdministrator())
        {
            throw new UnauthorizedAccessException(CliCommandStrings.InsufficientPrivilegeToStartServer);
        }

        // Best effort to verify that the server was not started indirectly or being spoofed.
        if (ParentProcess == null || ParentProcess.StartTime > CurrentProcess.StartTime ||
            !string.Equals(ParentProcess.MainModule.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(string.Format(CliCommandStrings.NoTrustWithParentPID, ParentProcess?.Id));
        }

        // Configure pipe DACLs
        SecurityIdentifier authenticatedUserIdentifier = new(WellKnownSidType.AuthenticatedUserSid, null);
        SecurityIdentifier currentOwnerIdentifier = WindowsIdentity.GetCurrent().Owner;
        PipeSecurity pipeSecurity = new();

        // The current user has full control and should be running as Administrator.
        pipeSecurity.SetOwner(currentOwnerIdentifier);
        pipeSecurity.AddAccessRule(new PipeAccessRule(currentOwnerIdentifier, PipeAccessRights.FullControl, AccessControlType.Allow));

        // Restrict read/write access to authenticated users
        pipeSecurity.AddAccessRule(new PipeAccessRule(authenticatedUserIdentifier,
            PipeAccessRights.Read | PipeAccessRights.Write | PipeAccessRights.Synchronize, AccessControlType.Allow));

        // Initialize the named pipe for dispatching commands. The name of the pipe is based off the server PID since
        // the client knows this value and ensures both processes can generate the same name.
        string pipeName = WindowsUtils.CreatePipeName(CurrentProcess.Id);
        NamedPipeServerStream serverPipe = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message,
            PipeOptions.None, 65535, 65535, pipeSecurity);

        // The client process will generate the actual log file. The server will log messages through a separate pipe.
        string logPipeName = WindowsUtils.CreatePipeName(CurrentProcess.Id, "log");
        NamedPipeServerStream logPipe = NamedPipeServerStreamAcl.Create(logPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message,
            PipeOptions.None, 65535, 65535, pipeSecurity);
        PipeStreamSetupLogger logger = new(logPipe, logPipeName);
        InstallServerElevationContext elevationContext = new(serverPipe);

        return new NetSdkMsiInstallerServer(elevationContext, logger, verifySignatures);
    }
}
