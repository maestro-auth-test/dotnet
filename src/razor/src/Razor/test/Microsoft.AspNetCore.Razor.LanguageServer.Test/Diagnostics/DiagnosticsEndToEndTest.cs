﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if !NET
using System.Collections.Generic;
#endif
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting.Diagnostics;
using Microsoft.CodeAnalysis.Razor.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Diagnostics;

public sealed class DiagnosticsEndToEndTest(ITestOutputHelper testOutput) : SingleServerDelegatingEndpointTestBase(testOutput)
{
    [Fact]
    public async Task Handle()
    {
        var input = """

            <div></div>

            @functions
            {
                public void M()
                {
                    {|CS0103:CallOnMe|}();
                }
            }

            """;

        await ValidateDiagnosticsAsync(input);
    }

    [Fact]
    public async Task Handle_Razor()
    {
        var input = """

            {|RZ10012:<NonExistentComponent />|}

            """;

        await ValidateDiagnosticsAsync(input, "File.razor");
    }

    [Fact]
    public async Task TODOComment()
    {
        var input = """

            <div></div>

            @functions
            {
                public void M()
                {
                    // {|TODO:|}TODO: This should be done
                }
            }

            """;

        await ValidateDiagnosticsAsync(input);
    }

    private async Task ValidateDiagnosticsAsync(string input, string? filePath = null)
    {
        TestFileMarkupParser.GetSpans(input, out input, out ImmutableDictionary<string, ImmutableArray<TextSpan>> spans);

        var codeDocument = CreateCodeDocument(input, filePath: filePath);
        var sourceText = codeDocument.Source.Text;
        var razorFilePath = "file://C:/path/test.razor";
        var uri = new Uri(razorFilePath);
        await using var languageServer = await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var requestContext = new RazorRequestContext(documentContext, null!, "lsp/method", uri: null);

        var translateDiagnosticsService = new RazorTranslateDiagnosticsService(DocumentMappingService, LoggerFactory);
        var optionsMonitor = TestRazorLSPOptionsMonitor.Create();
        var diagnosticsEndPoint = new DocumentDiagnosticsEndpoint(translateDiagnosticsService, languageServer, telemetryReporter: null);

        var diagnosticsRequest = new DocumentDiagnosticParams
        {
            TextDocument = new TextDocumentIdentifier { DocumentUri = new(uri) }
        };

        var report = await diagnosticsEndPoint.HandleRequestAsync(diagnosticsRequest, requestContext, DisposalToken);
        var fullReport = Assert.IsType<FullDocumentDiagnosticReport>(report);

        // Because the test razor project isn't set up properly, we get some extra diagnostics that we don't care about
        // so lets just validate that we get the ones we expect. We're testing the communication and translation between
        // Razor and C# after all, not whether our test infra can create a fully working project with all references.
        foreach (var (code, span) in spans)
        {
            // If any future test requires multiple diagnostics of the same type, please change this code :)
            var diagnostic = Assert.Single(fullReport.Items, d => d.Code == code);
            Assert.Equal(span.First(), sourceText.GetTextSpan(diagnostic.Range));
        }
    }
}
