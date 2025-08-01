﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;

internal partial class TestProjectSnapshotManager
{
    public partial class Listener
    {
        public readonly struct Inspector(ProjectChangeEventArgs notification)
        {
            private void CommonAssertions(ProjectChangeKind kind, ProjectKey? projectKey = null, bool? isSolutionClosing = null)
            {
                Assert.Equal(kind, notification.Kind);

                if (projectKey is ProjectKey k)
                {
                    Assert.Equal(k, notification.ProjectKey);
                }

                if (isSolutionClosing is bool b)
                {
                    Assert.Equal(b, notification.IsSolutionClosing);
                }
            }

            private void DocumentAssertions(string? documentFilePath = null)
            {
                if (documentFilePath is string s)
                {
                    Assert.Equal(s, notification.DocumentFilePath);
                }
            }

            private void ProjectAssertions(string? projectFilePath = null)
            {
                if (projectFilePath is string s)
                {
                    Assert.Equal(s, notification.ProjectFilePath);
                }
            }

            public void DocumentAdded(string? documentFilePath = null, ProjectKey? projectKey = null, bool? solutionIsClosing = null)
            {
                CommonAssertions(ProjectChangeKind.DocumentAdded, projectKey, solutionIsClosing);
                DocumentAssertions(documentFilePath);
            }

            public void DocumentRemoved(string? documentFilePath = null, ProjectKey? projectKey = null, bool? solutionIsClosing = null)
            {
                CommonAssertions(ProjectChangeKind.DocumentRemoved, projectKey, solutionIsClosing);
                DocumentAssertions(documentFilePath);
            }

            public void DocumentChanged(string? documentFilePath = null, ProjectKey? projectKey = null, bool? solutionIsClosing = null)
            {
                CommonAssertions(ProjectChangeKind.DocumentChanged, projectKey, solutionIsClosing);
                DocumentAssertions(documentFilePath);
            }

            public void ProjectAdded(string? projectFilePath = null, ProjectKey? projectKey = null, bool? solutionIsClosing = null)
            {
                CommonAssertions(ProjectChangeKind.ProjectAdded, projectKey, solutionIsClosing);
                ProjectAssertions(projectFilePath);
            }

            public void ProjectRemoved(string? projectFilePath = null, ProjectKey? projectKey = null, bool? solutionIsClosing = null)
            {
                CommonAssertions(ProjectChangeKind.ProjectRemoved, projectKey, solutionIsClosing);
                ProjectAssertions(projectFilePath);
            }

            public void ProjectChanged(string? projectFilePath = null, ProjectKey? projectKey = null, bool? solutionIsClosing = null)
            {
                CommonAssertions(ProjectChangeKind.ProjectChanged, projectKey, solutionIsClosing);
                ProjectAssertions(projectFilePath);
            }
        }
    }
}
