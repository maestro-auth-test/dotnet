﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.CodeActions;

internal interface ICSharpCodeActionResolver : ICodeActionResolver
{
    Task<CodeAction> ResolveAsync(DocumentContext documentContext, CodeAction codeAction, CancellationToken cancellationToken);
}
