﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.CodeAnalysis.Razor.Protocol.DocumentPresentation;

/// <summary>
/// Class representing the parameters sent for a textDocument/_vs_textPresentation request.
/// </summary>
internal class TextPresentationParams : VSInternalTextPresentationParams, IPresentationParams
{
}
