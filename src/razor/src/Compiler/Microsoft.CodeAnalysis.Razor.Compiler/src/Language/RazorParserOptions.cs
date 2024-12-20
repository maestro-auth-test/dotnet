﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RazorParserOptions
{
    public static RazorParserOptions CreateDefault()
    {
        return new RazorParserOptions(
            Array.Empty<DirectiveDescriptor>(),
            designTime: false,
            parseLeadingDirectives: false,
            useRoslynTokenizer: false,
            version: RazorLanguageVersion.Latest,
            fileKind: FileKinds.Legacy,
            enableSpanEditHandlers: false,
            csharpParseOptions: CSharpParseOptions.Default);
    }

    public static RazorParserOptions Create(Action<RazorParserOptionsBuilder> configure)
    {
        return Create(configure, fileKind: FileKinds.Legacy);
    }

    public static RazorParserOptions Create(Action<RazorParserOptionsBuilder> configure, string fileKind)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var builder = new RazorParserOptionsBuilder(designTime: false, version: RazorLanguageVersion.Latest, fileKind ?? FileKinds.Legacy);
        configure(builder);
        var options = builder.Build();

        return options;
    }

    public static RazorParserOptions CreateDesignTime(Action<RazorParserOptionsBuilder> configure)
    {
        return CreateDesignTime(configure, fileKind: null);
    }

    public static RazorParserOptions CreateDesignTime(Action<RazorParserOptionsBuilder> configure, string fileKind)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var builder = new RazorParserOptionsBuilder(designTime: true, version: RazorLanguageVersion.Latest, fileKind ?? FileKinds.Legacy);
        configure(builder);
        var options = builder.Build();

        return options;
    }

    internal RazorParserOptions(DirectiveDescriptor[] directives, bool designTime, bool parseLeadingDirectives, bool useRoslynTokenizer, RazorLanguageVersion version, string fileKind, bool enableSpanEditHandlers, CSharpParseOptions csharpParseOptions)
    {
        if (directives == null)
        {
            throw new ArgumentNullException(nameof(directives));
        }

        if (parseLeadingDirectives && useRoslynTokenizer)
        {
            throw new ArgumentException($"Cannot set {nameof(parseLeadingDirectives)} and {nameof(useRoslynTokenizer)} to true simultaneously.");
        }

        Directives = directives;
        DesignTime = designTime;
        ParseLeadingDirectives = parseLeadingDirectives;
        UseRoslynTokenizer = useRoslynTokenizer;
        Version = version;
        FeatureFlags = RazorParserFeatureFlags.Create(Version, fileKind);
        FileKind = fileKind;
        EnableSpanEditHandlers = enableSpanEditHandlers;
        CSharpParseOptions = csharpParseOptions;
    }

    public bool DesignTime { get; }

    public IReadOnlyCollection<DirectiveDescriptor> Directives { get; }

    /// <summary>
    /// Gets a value which indicates whether the parser will parse only the leading directives. If <c>true</c>
    /// the parser will halt at the first HTML content or C# code block. If <c>false</c> the whole document is parsed.
    /// </summary>
    /// <remarks>
    /// Currently setting this option to <c>true</c> will result in only the first line of directives being parsed.
    /// In a future release this may be updated to include all leading directive content.
    /// </remarks>
    public bool ParseLeadingDirectives { get; }

    public bool UseRoslynTokenizer { get; }

    public CSharpParseOptions CSharpParseOptions { get; }

    public RazorLanguageVersion Version { get; } = RazorLanguageVersion.Latest;

    internal string FileKind { get; }

    internal RazorParserFeatureFlags FeatureFlags { get; /* Testing Only */ init; }

    internal bool EnableSpanEditHandlers { get; }
}
