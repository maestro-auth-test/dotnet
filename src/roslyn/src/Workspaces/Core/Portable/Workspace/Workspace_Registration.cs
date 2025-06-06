﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.WorkspaceEventMap;

namespace Microsoft.CodeAnalysis;

/* This is the static API on Workspace that lets you associate text containers with workspace instances */
public abstract partial class Workspace
{
    private static readonly ConditionalWeakTable<SourceTextContainer, WorkspaceRegistration> s_bufferToWorkspaceRegistrationMap = new();

    /// <summary>
    /// Gets the workspace associated with the specific text container.
    /// </summary>
    public static bool TryGetWorkspace(SourceTextContainer textContainer, [NotNullWhen(true)] out Workspace? workspace)
    {
        if (textContainer == null)
        {
            throw new ArgumentNullException(nameof(textContainer));
        }

        var registration = GetWorkspaceRegistration(textContainer);
        workspace = registration.Workspace;

        return workspace != null;
    }

    /// <summary>
    /// Register a correspondence between a text container and a workspace.
    /// </summary>
    protected void RegisterText(SourceTextContainer textContainer)
    {
        if (textContainer == null)
        {
            throw new ArgumentNullException(nameof(textContainer));
        }

        var registration = GetWorkspaceRegistration(textContainer);
        registration.SetWorkspace(this);

        // Require main thread on the callback as WorkspaceRegistration.RaiseEvents invokes Workspace.WorkspaceChanges which is publicly exposed
        // and the event handlers may have main thread dependencies. Potential cleanup here with relation to
        // https://github.com/dotnet/roslyn/issues/32551
        var handlerAndOptions = new WorkspaceEventHandlerAndOptions(args => registration.RaiseEvents(), WorkspaceEventOptions.RequiresMainThreadOptions);
        var handlerSet = EventHandlerSet.Create(handlerAndOptions);
        this.ScheduleTask(EventArgs.Empty, handlerSet);
    }

    /// <summary>
    /// Unregister a correspondence between a text container and a workspace.
    /// </summary>
    protected void UnregisterText(SourceTextContainer textContainer)
    {
        if (textContainer == null)
        {
            throw new ArgumentNullException(nameof(textContainer));
        }

        var registration = GetWorkspaceRegistration(textContainer);

        if (registration.Workspace == this)
        {
            registration.SetWorkspaceAndRaiseEvents(null);
        }
    }

    /// <summary>
    /// Returns a <see cref="WorkspaceRegistration" /> for a given text container.
    /// </summary>
    public static WorkspaceRegistration GetWorkspaceRegistration(SourceTextContainer textContainer)
    {
        if (textContainer == null)
        {
            throw new ArgumentNullException(nameof(textContainer));
        }

        return s_bufferToWorkspaceRegistrationMap.GetValue(textContainer, static _ => new WorkspaceRegistration());
    }
}
