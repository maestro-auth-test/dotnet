﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.ProjectSystem;

namespace Microsoft.VisualStudio.Razor.ProjectSystem;

// This defines the set of services that we frequently need for working with UnconfiguredProject.
//
// We're following a somewhat common pattern for code that uses CPS. It's really easy to end up
// relying on service location inside CPS, which can be hard to test. This approach makes it easy
// for us to build reusable mocks instead.
internal interface IUnconfiguredProjectCommonServices
{
    IProjectAsynchronousTasksService TasksService { get; }

    IProjectThreadingService ThreadingService { get; }

    UnconfiguredProject UnconfiguredProject { get; }

    IProjectFaultHandlerService FaultHandlerService { get; }

    IActiveConfigurationGroupSubscriptionService ActiveConfigurationGroupSubscriptionService { get; }
}
