﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class PublishedPathCommandResolver(
    IEnvironmentProvider environment,
    IPublishedPathCommandSpecFactory commandSpecFactory) : ICommandResolver
{
    private const string PublishedPathCommandResolverName = "PublishedPathCommandResolver";

    private readonly IEnvironmentProvider _environment = environment;
    private readonly IPublishedPathCommandSpecFactory _commandSpecFactory = commandSpecFactory;

    public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
    {
        var publishDirectory = commandResolverArguments.OutputPath;
        var commandName = commandResolverArguments.CommandName;
        var applicationName = commandResolverArguments.ApplicationName;

        if (publishDirectory == null || commandName == null || applicationName == null)
        {
            return null;
        }

        var commandPath = ResolveCommandPath(publishDirectory, commandName);

        if (commandPath == null)
        {
            return null;
        }

        var depsFilePath = Path.Combine(publishDirectory, $"{applicationName}.deps.json");
        if (!File.Exists(depsFilePath))
        {
            Reporter.Verbose.WriteLine(string.Format(
                CliStrings.DoesNotExist,
                PublishedPathCommandResolverName,
                depsFilePath));
            return null;
        }

        var runtimeConfigPath = Path.Combine(publishDirectory, $"{applicationName}.runtimeconfig.json");
        if (!File.Exists(runtimeConfigPath))
        {
            Reporter.Verbose.WriteLine(string.Format(
                CliStrings.DoesNotExist,
                PublishedPathCommandResolverName,
                runtimeConfigPath));
            return null;
        }

        return _commandSpecFactory.CreateCommandSpecFromPublishFolder(
                commandPath,
                commandResolverArguments.CommandArguments.OrEmptyIfNull(),
                depsFilePath,
                runtimeConfigPath);
    }

    private string ResolveCommandPath(string publishDirectory, string commandName)
    {
        if (!Directory.Exists(publishDirectory))
        {
            Reporter.Verbose.WriteLine(string.Format(
                CliStrings.DoesNotExist,
                PublishedPathCommandResolverName,
                publishDirectory));
            return null;
        }

        return _environment.GetCommandPathFromRootPath(publishDirectory, commandName, ".dll");
    }
}
