﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.VisualStudio.Text;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.LegacyEditor;

internal interface IRazorCodeDocument
{
    ImmutableArray<ClassifiedSpan> GetClassifiedSpans();
    ImmutableArray<TagHelperSpan> GetTagHelperSpans();

    ImmutableArray<RazorSourceMapping> GetSourceMappings();
    ImmutableArray<IRazorDiagnostic> GetDiagnostics();
    IRazorTagHelperDocumentContext? GetTagHelperContext();

    int? GetDesiredIndentation(ITextSnapshot snapshot, ITextSnapshotLine line, int indentSize, int tabSize);

    string GetGeneratedCode();
}
