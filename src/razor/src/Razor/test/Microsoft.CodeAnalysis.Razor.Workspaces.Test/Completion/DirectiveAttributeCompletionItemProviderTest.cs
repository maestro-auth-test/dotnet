﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.IntegrationTests;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Razor.Completion;

public class DirectiveAttributeCompletionItemProviderTest : RazorToolingIntegrationTestBase
{
    private readonly DirectiveAttributeCompletionItemProvider _provider;
    private readonly TagHelperDocumentContext _defaultTagHelperContext;

    internal override RazorFileKind? FileKind => RazorFileKind.Component;
    internal override bool UseTwoPhaseCompilation => true;

    public DirectiveAttributeCompletionItemProviderTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _provider = new DirectiveAttributeCompletionItemProvider();

        // Most of these completions rely on stuff in the web namespace.
        ImportItems.Add(CreateProjectItem(
            "_Imports.razor",
            "@using Microsoft.AspNetCore.Components.Web"));

        var codeDocument = GetCodeDocument(string.Empty);
        _defaultTagHelperContext = codeDocument.GetRequiredTagHelperContext();
    }

    private RazorCodeDocument GetCodeDocument(string content)
    {
        var result = CompileToCSharp(content, throwOnFailure: false);
        return result.CodeDocument;
    }

    [Fact]
    public void GetCompletionItems_OnNonAttributeArea_ReturnsEmptyCollection()
    {
        // Arrange
        var context = CreateRazorCompletionContext(absoluteIndex: 3, "<input @  />");

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetCompletionItems_OnDirectiveAttributeParameter_ReturnsEmptyCollection()
    {
        // Arrange
        var context = CreateRazorCompletionContext(absoluteIndex: 14, "<input @bind:fo  />");

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetCompletionItems_OnDirectiveAttributeName_bind_ReturnsCompletions()
    {
        // Arrange
        var context = CreateRazorCompletionContext(absoluteIndex: 8, "<input @  />");

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        AssertContains(completions, "bind", "@bind", ["=", ":"]);
    }

    [Fact]
    public void GetCompletionItems_OnDirectiveAttributeName_attributes_ReturnsCompletions()
    {
        // Arrange
        var context = CreateRazorCompletionContext(absoluteIndex: 8, "<input @  />");

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        AssertContains(completions, "attributes", "@attributes", ["="]);
    }

    [Fact]
    public void GetCompletionItems_AttributeAreaEndOfSelfClosingTag_ReturnsEmptyCollection()
    {
        // Arrange
        var context = CreateRazorCompletionContext(absoluteIndex: 16, "<input @bind:fo  />");

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetCompletionItems_AttributeAreaEndOfOpeningTag_ReturnsEmptyCollection()
    {
        // Arrange
        var context = CreateRazorCompletionContext(absoluteIndex: 16, "<input @bind:fo   ></input>");

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetCompletionItems_ExistingAttribute_LeadingEdge_ReturnsEmptyCollection()
    {
        // Arrange
        var context = CreateRazorCompletionContext(absoluteIndex: 7, "<input src=\"xyz\" />");

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetCompletionItems_ExistingAttribute_TrailingEdge_ReturnsEmptyCollection()
    {
        // Arrange
        var context = CreateRazorCompletionContext(absoluteIndex: 16, "<input src=\"xyz\" />");

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetCompletionItems_ExistingAttribute_Partial_ReturnsEmptyCollection()
    {
        // Arrange
        var context = CreateRazorCompletionContext(absoluteIndex: 9, "<svg xml: ></svg>");

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetAttributeCompletions_NoDescriptorsForTag_ReturnsEmptyCollection()
    {
        // Arrange
        var documentContext = TagHelperDocumentContext.Create(string.Empty, tagHelpers: []);

        // Act
        var completions = DirectiveAttributeCompletionItemProvider.GetAttributeCompletions("@bin", "foobarbaz", [], documentContext);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetAttributeCompletions_NoDirectiveAttributesForTag_ReturnsEmptyCollection()
    {
        // Arrange
        var descriptor = TagHelperDescriptorBuilder.Create("CatchAll", "TestAssembly");
        descriptor.BoundAttributeDescriptor(boundAttribute => boundAttribute.Name = "Test");
        descriptor.TagMatchingRule(rule => rule.RequireTagName("*"));
        var documentContext = TagHelperDocumentContext.Create(string.Empty, [descriptor.Build()]);

        // Act
        var completions = DirectiveAttributeCompletionItemProvider.GetAttributeCompletions("@bin", "input", [], documentContext);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetAttributeCompletions_SelectedDirectiveAttribute_IsIncludedInCompletions()
    {
        // Arrange
        var attributeNames = ImmutableArray.Create("@bind");

        // Act
        var completions = DirectiveAttributeCompletionItemProvider.GetAttributeCompletions("@bind", "input", attributeNames, _defaultTagHelperContext);

        // Assert
        AssertContains(completions, "bind", "@bind", ["=", ":"]);
    }

    [Fact]
    public void GetAttributeCompletions_NonIndexer_ReturnsCompletion()
    {
        // Arrange

        // Act
        var completions = DirectiveAttributeCompletionItemProvider.GetAttributeCompletions("@", "input", [], _defaultTagHelperContext);

        // Assert
        AssertContains(completions, "bind", "@bind", ["=", ":"]);
    }

    [Fact]
    public void GetAttributeCompletions_Indexer_ReturnsCompletion()
    {
        // Arrange

        // Act
        var completions = DirectiveAttributeCompletionItemProvider.GetAttributeCompletions("@", "input", [], _defaultTagHelperContext);

        // Assert
        AssertContains(completions, "bind-", "@bind-...", []);
    }

    [Fact]
    public void GetAttributeCompletions_BaseDirectiveAttributeAlreadyExists_IncludesBaseAttribute()
    {
        // Arrange
        var attributeNames = ImmutableArray.Create("@bind", "@");

        // Act
        var completions = DirectiveAttributeCompletionItemProvider.GetAttributeCompletions("@", "input", attributeNames, _defaultTagHelperContext);

        // Assert
        AssertContains(completions, "bind", "@bind", ["=", ":"]);
    }

    [Fact]
    public void GetAttributeCompletions_BaseDirectiveAttributeAndParameterVariationsExist_ExcludesCompletion()
    {
        // Arrange
        var attributeNames = ImmutableArray.Create(
            "@bind",
            "@bind:format",
            "@bind:event",
            "@bind:culture",
            "@bind:get",
            "@bind:set",
            "@bind:after",
            "@");

        // Act
        var completions = DirectiveAttributeCompletionItemProvider.GetAttributeCompletions("@", "input", attributeNames, _defaultTagHelperContext);

        // Assert
        AssertDoesNotContain(completions, "bind", "@bind");
    }

    private static void AssertContains(ImmutableArray<RazorCompletionItem> completions, string insertText, string displayText, ImmutableArray<string> commitCharacters)
    {
        displayText ??= insertText;

        Assert.Contains(completions, completion =>
            insertText == completion.InsertText &&
            displayText == completion.DisplayText &&
            commitCharacters.SequenceEqual(completion.CommitCharacters.Select(c => c.Character)) &&
            RazorCompletionItemKind.DirectiveAttribute == completion.Kind);
    }

    private static void AssertDoesNotContain(IReadOnlyList<RazorCompletionItem> completions, string insertText, string displayText)
    {
        displayText ??= insertText;

        Assert.DoesNotContain(completions, completion => insertText == completion.InsertText &&
               displayText == completion.DisplayText &&
               RazorCompletionItemKind.DirectiveAttribute == completion.Kind);
    }

    private RazorCompletionContext CreateRazorCompletionContext(int absoluteIndex, string documentContent)
    {
        var codeDocument = GetCodeDocument(documentContent);
        var syntaxTree = codeDocument.GetRequiredSyntaxTree();
        var tagHelperContext = codeDocument.GetRequiredTagHelperContext();

        var owner = syntaxTree.Root.FindInnermostNode(absoluteIndex, includeWhitespace: true, walkMarkersBack: true);
        owner = AbstractRazorCompletionFactsService.AdjustSyntaxNodeForWordBoundary(owner, absoluteIndex);

        return new RazorCompletionContext(absoluteIndex, owner, syntaxTree, tagHelperContext);
    }
}
