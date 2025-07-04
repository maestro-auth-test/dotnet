﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.CommandFactory;

public static class CommandFactoryUsingResolver
{
    private static readonly string[] _knownCommandsAvailableAsDotNetTool = ["dotnet-dev-certs", "dotnet-sql-cache", "dotnet-user-secrets", "dotnet-watch", "dotnet-user-jwts"];

    public static Command CreateDotNet(
        string commandName,
        IEnumerable<string> args,
        NuGetFramework framework = null,
        string configuration = Constants.DefaultConfiguration,
        string currentWorkingDirectory = null)
    {
        return Create("dotnet",
            new[] { commandName }.Concat(args),
            framework,
            configuration: configuration,
            currentWorkingDirectory);
    }

    /// <summary>
    /// Create a command with the specified arg array. Args will be 
    /// escaped properly to ensure that exactly the strings in this
    /// array will be present in the corresponding argument array
    /// in the command's process.
    /// </summary>
    public static Command Create(
        string commandName,
        IEnumerable<string> args,
        NuGetFramework framework = null,
        string configuration = Constants.DefaultConfiguration,
        string outputPath = null,
        string applicationName = null,
        string currentWorkingDirectory = null)
    {
        return Create(
            new DefaultCommandResolverPolicy(),
            commandName,
            args,
            framework,
            configuration,
            outputPath,
            applicationName,
            currentWorkingDirectory);
    }

    public static Command Create(
        ICommandResolverPolicy commandResolverPolicy,
        string commandName,
        IEnumerable<string> args,
        NuGetFramework framework = null,
        string configuration = Constants.DefaultConfiguration,
        string outputPath = null,
        string applicationName = null,
        string currentWorkingDirectory = null)
    {
        var commandSpec = CommandResolver.TryResolveCommandSpec(
            commandResolverPolicy,
            commandName,
            args,
            framework,
            configuration: configuration,
            outputPath: outputPath,
            applicationName: applicationName,
            currentWorkingDirectory: currentWorkingDirectory);

        return CreateOrThrow(commandName, commandSpec);
    }

#nullable enable
    public static Command CreateOrThrow(string commandName, CommandSpec? commandSpec)
    {
        if (commandSpec == null)
        {
            if (_knownCommandsAvailableAsDotNetTool.Contains(commandName, StringComparer.OrdinalIgnoreCase))
            {
                throw new CommandAvailableAsDotNetToolException(commandName);
            }
            else
            {
                throw new CommandUnknownException(commandName);
            }
        }

        var command = Create(commandSpec);

        return command;
    }
#nullable disable

    public static Command Create(CommandSpec commandSpec)
    {
        var psi = new ProcessStartInfo
        {
            FileName = commandSpec.Path,
            Arguments = commandSpec.Args,
            UseShellExecute = false
        };

        foreach (var environmentVariable in commandSpec.EnvironmentVariables)
        {
            if (!psi.Environment.ContainsKey(environmentVariable.Key))
            {
                psi.Environment.Add(environmentVariable.Key, environmentVariable.Value);
            }
        }

        var _process = new Process
        {
            StartInfo = psi
        };

        return new Command(_process, customEnvironmentVariables: commandSpec.EnvironmentVariables);
    }
}
