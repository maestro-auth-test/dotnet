// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Versioning;

[assembly:UnsupportedOSPlatform("windows")]

namespace Microsoft.DotNet.SourceBuild.Tests;

internal static class Config
{
    const string ConfigSwitchPrefix = "Microsoft.DotNet.SourceBuild.Tests.";

    public static string DotNetDirectory => (string)AppContext.GetData(ConfigSwitchPrefix + nameof(DotNetDirectory))! ?? throw new InvalidOperationException("DotNetDirectory must be specified");
    public static string PortableRid => (string)AppContext.GetData(ConfigSwitchPrefix + nameof(PortableRid))! ?? throw new InvalidOperationException("Portable RID must be specified");
    public static string TargetRid => (string)AppContext.GetData(ConfigSwitchPrefix + nameof(TargetRid))! ?? throw new InvalidOperationException("Target RID must be specified");
    public static string LogsDirectory => (string)AppContext.GetData(ConfigSwitchPrefix + nameof(LogsDirectory))! ?? throw new InvalidOperationException("Logs directory must be specified");

    public static string? CustomPackagesPath => (string)AppContext.GetData(ConfigSwitchPrefix + nameof(CustomPackagesPath))!;
    public static string? LicenseScanPath => (string)AppContext.GetData(ConfigSwitchPrefix + nameof(LicenseScanPath))!;
    public static string? MsftSdkTarballPath => (string)AppContext.GetData(ConfigSwitchPrefix + nameof(MsftSdkTarballPath))!;
    public static string? PoisonReportPath => (string)AppContext.GetData(ConfigSwitchPrefix + nameof(PoisonReportPath))!;
    public static string? SdkTarballPath => (string)AppContext.GetData(ConfigSwitchPrefix + nameof(SdkTarballPath))!;
    public static string? SourceBuiltArtifactsPath => (string)AppContext.GetData(ConfigSwitchPrefix + nameof(SourceBuiltArtifactsPath))!;
    public static string? RestoreAdditionalProjectSources => (string?)AppContext.GetData(ConfigSwitchPrefix + nameof(RestoreAdditionalProjectSources));
    public static bool IsOfficialBuild => bool.TryParse((string)AppContext.GetData(ConfigSwitchPrefix + nameof(IsOfficialBuild))!, out bool isOfficialBuild) && isOfficialBuild;

    // Indicates whether the tests are being run in the context of a CI pipeline
    public static bool RunningInCI => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_CI")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_OS"));

    public static string TargetArchitecture => TargetRid.Split('-')[1];
}
