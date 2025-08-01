﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockInstallerFactory : IInstallerFactory
    {
        private readonly Guid _factoryId = new Guid("00000000-0000-0000-0000-000000000000");

        public string Name => "MockInstallerFactory";

        public Guid Id => _factoryId;

        public IInstaller CreateInstaller(IEngineEnvironmentSettings settings, string installPath) => throw new NotImplementedException();
    }
}
