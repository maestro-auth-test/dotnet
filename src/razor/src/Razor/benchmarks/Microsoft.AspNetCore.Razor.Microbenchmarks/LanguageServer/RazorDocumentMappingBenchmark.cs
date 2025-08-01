﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.LanguageServer;

public class RazorDocumentMappingBenchmark : RazorLanguageServerBenchmarkBase
{
    private string _filePath;

    private IDocumentMappingService DocumentMappingService { get; set; }

    private IDocumentSnapshot DocumentSnapshot { get; set; }

    private RazorCSharpDocument CSharpDocument { get; set; }

    private List<int> Indexes { get; set; }

    /// <summary>
    /// How many blocks of 25 lines of code should be formatted
    /// </summary>
    [Params(1, 2, 3, 4, 5, 10, 20, 30, 40, 100)]
    public int Blocks { get; set; }

    [GlobalSetup]
    public async Task InitializeRazorCSharpFormattingAsync()
    {
        EnsureServicesInitialized();

        var projectRoot = Path.Combine(Helpers.GetTestAppsPath(), "ComponentApp");
        var projectFilePath = Path.Combine(projectRoot, "ComponentApp.csproj");
        _filePath = Path.Combine(projectRoot, "Components", "Pages", "Generated.razor");

        WriteSampleFile(_filePath, Blocks, out var indexes);

        Indexes = indexes;

        var targetPath = "/Components/Pages/Generated.razor";

        DocumentSnapshot = await GetDocumentSnapshotAsync(projectFilePath, _filePath, targetPath);

        var codeDocument = await DocumentSnapshot.GetGeneratedOutputAsync(CancellationToken.None);
        CSharpDocument = codeDocument.GetRequiredCSharpDocument();
    }

    private static void WriteSampleFile(string filePath, int blocks, out List<int> indexes)
    {
        indexes = new List<int>();
        var data = @"
@{
    y = 456;
}

<div>
    <span>@DateTime.Now</span>
    @if (true)
    {
        var x = 123;
        <span>@x</span>
    }
</div>

@code {
    public string Prop$INDEX$ { get; set; }

    public string[] SomeList$INDEX$ { get; set; }

    public class Foo$INDEX$
    {
        @* This is a Razor Comment *@
        void Method() { }
    }
}

";
        using var fileStream = File.CreateText(filePath);

        var index = 0;
        for (var i = 0; i < blocks; i++)
        {
            var fileSegment = data.Replace("$INDEX$", i.ToString());
            fileStream.WriteLine(fileSegment);
            index += fileSegment.Length;
            indexes.Add(index);
        }
    }

    [Benchmark]
    public LinePosition Loop()
    {
        LinePosition position = default;
        foreach (var index in Indexes)
        {
            TryMapToHostDocumentPosition(CSharpDocument, index, out position, out _);
        }

        return position;
    }

    [Benchmark]
    public LinePosition BinarySearch()
    {
        LinePosition position = default;
        foreach (var index in Indexes)
        {
            DocumentMappingService.TryMapToRazorDocumentPosition(CSharpDocument, index, out position, out _);
        }

        return position;
    }

    // old code, copied from RazorDocumentMappingService before making changes
    private bool TryMapToHostDocumentPosition(RazorCSharpDocument csharpDocument, int generatedDocumentIndex, out LinePosition hostDocumentPosition, out int hostDocumentIndex)
    {
        if (csharpDocument is null)
        {
            throw new ArgumentNullException(nameof(csharpDocument));
        }

        foreach (var mapping in csharpDocument.SourceMappings)
        {
            var generatedSpan = mapping.GeneratedSpan;
            var generatedAbsoluteIndex = generatedSpan.AbsoluteIndex;
            if (generatedAbsoluteIndex <= generatedDocumentIndex)
            {
                // Treat the mapping as owning the edge at its end (hence <= originalSpan.Length),
                // otherwise we wouldn't handle the cursor being right after the final C# char
                var distanceIntoGeneratedSpan = generatedDocumentIndex - generatedAbsoluteIndex;
                if (distanceIntoGeneratedSpan <= generatedSpan.Length)
                {
                    // Found the generated span that contains the generated absolute index

                    hostDocumentIndex = mapping.OriginalSpan.AbsoluteIndex + distanceIntoGeneratedSpan;
                    hostDocumentPosition = csharpDocument.CodeDocument.Source.Text.GetLinePosition(hostDocumentIndex);
                    return true;
                }
            }
        }

        hostDocumentPosition = default;
        hostDocumentIndex = default;
        return false;
    }

    [GlobalCleanup]
    public async Task CleanupServerAsync()
    {
        File.Delete(_filePath);

        var server = RazorLanguageServerHost.GetTestAccessor().Server;

        await server.ShutdownAsync();
        await server.ExitAsync();
    }

    private void EnsureServicesInitialized()
    {
        DocumentMappingService = RazorLanguageServerHost.GetRequiredService<IDocumentMappingService>();
    }
}
