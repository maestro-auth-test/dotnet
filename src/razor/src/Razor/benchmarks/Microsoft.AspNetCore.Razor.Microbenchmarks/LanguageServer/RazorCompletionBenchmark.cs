﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Telemetry;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.LanguageServer;

public class RazorCompletionBenchmark : RazorLanguageServerBenchmarkBase
{
    private string? _filePath;
    private Uri? DocumentUri { get; set; }
    private RazorCompletionEndpoint? CompletionEndpoint { get; set; }
    private IDocumentSnapshot? DocumentSnapshot { get; set; }
    private SourceText? DocumentText { get; set; }
    private Position? RazorPosition { get; set; }
    private RazorRequestContext RazorRequestContext { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        var razorCompletionListProvider = RazorLanguageServerHost.GetRequiredService<RazorCompletionListProvider>();
        var documentMappingService = RazorLanguageServerHost.GetRequiredService<IDocumentMappingService>();
        var clientConnection = RazorLanguageServerHost.GetRequiredService<IClientConnection>();
        var completionListCache = RazorLanguageServerHost.GetRequiredService<CompletionListCache>();
        var triggerAndCommitCharacters = RazorLanguageServerHost.GetRequiredService<CompletionTriggerAndCommitCharacters>();
        var loggerFactory = RazorLanguageServerHost.GetRequiredService<ILoggerFactory>();

        var delegatedCompletionListProvider = new TestDelegatedCompletionListProvider(documentMappingService, clientConnection, completionListCache, triggerAndCommitCharacters);
        var completionListProvider = new CompletionListProvider(razorCompletionListProvider, delegatedCompletionListProvider, triggerAndCommitCharacters);
        var configurationService = new DefaultRazorConfigurationService(clientConnection, loggerFactory);
        var optionsMonitor = new RazorLSPOptionsMonitor(configurationService, RazorLSPOptions.Default);
        CompletionEndpoint = new RazorCompletionEndpoint(completionListProvider, triggerAndCommitCharacters, NoOpTelemetryReporter.Instance, optionsMonitor);

        var clientCapabilities = new VSInternalClientCapabilities
        {
            TextDocument = new TextDocumentClientCapabilities
            {
                Completion = new VSInternalCompletionSetting
                {
                },
            },
        };
        CompletionEndpoint.ApplyCapabilities(new(), clientCapabilities);
        var projectRoot = Path.Combine(Helpers.GetTestAppsPath(), "ComponentApp");
        var projectFilePath = Path.Combine(projectRoot, "ComponentApp.csproj");
        _filePath = Path.Combine(projectRoot, "Components", "Pages", "Generated.razor");

        var content = GetFileContents();

        var razorCodeActionIndex = content.IndexOf("|R|");
        content = content.Replace("|R|", "");

        File.WriteAllText(_filePath, content);

        var targetPath = "/Components/Pages/Generated.razor";

        DocumentUri = new Uri(_filePath);
        DocumentSnapshot = await GetDocumentSnapshotAsync(projectFilePath, _filePath, targetPath);
        DocumentText = await DocumentSnapshot.GetTextAsync(CancellationToken.None);

        RazorPosition = DocumentText.GetPosition(razorCodeActionIndex);

        var documentContext = new DocumentContext(DocumentUri, DocumentSnapshot, projectContext: null);
        RazorRequestContext = new RazorRequestContext(
            documentContext,
            RazorLanguageServerHost.GetRequiredService<LspServices>(),
            "lsp/method",
            uri: null);
    }

    private static string GetFileContents()
    {
        var sb = new StringBuilder();

        sb.Append("""
            @using System;
            @using Endpoints.Pages;
            """);

        for (var i = 0; i < 100; i++)
        {
            sb.Append($$"""
            @{
                var y{{i}} = 456;
            }
            <div>
                <p>Hello there Mr {{i}}</p>
            </div>
            """);
        }

        sb.Append("""
            <div>
                <Ind|R|
                <span>@DateTime.Now</span>
                @if (true)
                {
                    <span>@y</span>
                }
            </div>
            @code {
                private void Goo()
                {
                }
            }"
            """);

        return sb.ToString();
    }

    [Benchmark(Description = "Razor Completion")]
    public async Task RazorCompletionAsync()
    {
        var completionParams = new CompletionParams
        {
            Position = RazorPosition!,
            Context = new VSInternalCompletionContext { },
            TextDocument = new TextDocumentIdentifier
            {
                DocumentUri = new(DocumentUri!),
            },
        };

        var _ = await CompletionEndpoint!.HandleRequestAsync(completionParams, RazorRequestContext, CancellationToken.None);
    }

    private class TestDelegatedCompletionListProvider : DelegatedCompletionListProvider
    {
        public TestDelegatedCompletionListProvider(
            IDocumentMappingService documentMappingService,
            IClientConnection clientConnection,
            CompletionListCache completionListCache,
            CompletionTriggerAndCommitCharacters triggerAndCommitCharacters)
            : base(documentMappingService, clientConnection, completionListCache, triggerAndCommitCharacters)
        {
        }

        public override ValueTask<RazorVSInternalCompletionList?> GetCompletionListAsync(
            RazorCodeDocument codeDocument,
            int absoluteIndex,
            VSInternalCompletionContext completionContext,
            DocumentContext documentContext,
            VSInternalClientCapabilities clientCapabilities,
            RazorCompletionOptions completionOptions,
            Guid correlationId,
            CancellationToken cancellationToken)
            => new(new RazorVSInternalCompletionList() { Items = [] });
    }
}
