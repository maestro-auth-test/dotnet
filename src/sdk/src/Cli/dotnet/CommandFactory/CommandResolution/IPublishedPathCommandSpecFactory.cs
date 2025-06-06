﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public interface IPublishedPathCommandSpecFactory
{
    CommandSpec CreateCommandSpecFromPublishFolder(
        string commandPath,
        IEnumerable<string> commandArguments,
        string depsFilePath,
        string runtimeConfigPath);
}
