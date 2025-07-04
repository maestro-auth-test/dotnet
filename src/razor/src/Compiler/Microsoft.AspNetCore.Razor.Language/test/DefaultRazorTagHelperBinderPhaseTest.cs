﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Razor.Language;

public class DefaultRazorTagHelperContextDiscoveryPhaseTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Latest;

    #region Legacy
    [Fact]
    public void Execute_CanHandleSingleLengthAddTagHelperDirective()
    {
        // Arrange
        var expectedDiagnostics = new[]
        {
            RazorDiagnosticFactory.CreateParsing_UnterminatedStringLiteral(
                new SourceSpan(new SourceLocation(14 + Environment.NewLine.Length, 1, 14), contentLength: 1)),
            RazorDiagnosticFactory.CreateParsing_InvalidTagHelperLookupText(
                new SourceSpan(new SourceLocation(14 + Environment.NewLine.Length, 1, 14), contentLength: 1), "\"")
        };

        var content =
        @"
@addTagHelper """;
        var source = TestRazorSourceDocument.Create(content, filePath: null);
        var codeDocument = ProjectEngine.CreateCodeDocument(source);
        var originalTree = RazorSyntaxTree.Parse(source);
        codeDocument.SetSyntaxTree(originalTree);

        // Act
        ProjectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);

        // Assert
        var rewrittenTree = codeDocument.GetSyntaxTree();
        var erroredNode = rewrittenTree.Root.DescendantNodes().First(n => n.GetChunkGenerator() is AddTagHelperChunkGenerator);
        var chunkGenerator = Assert.IsType<AddTagHelperChunkGenerator>(erroredNode.GetChunkGenerator());
        Assert.Equal(expectedDiagnostics, chunkGenerator.Diagnostics);
    }

    [Fact]
    public void Execute_CanHandleSingleLengthRemoveTagHelperDirective()
    {
        // Arrange
        var expectedDiagnostics = new[]
        {
            RazorDiagnosticFactory.CreateParsing_UnterminatedStringLiteral(
                new SourceSpan(new SourceLocation(17 + Environment.NewLine.Length, 1, 17), contentLength: 1)),
            RazorDiagnosticFactory.CreateParsing_InvalidTagHelperLookupText(
                new SourceSpan(new SourceLocation(17 + Environment.NewLine.Length, 1, 17), contentLength: 1), "\"")
        };

        var content =
        @"
@removeTagHelper """;
        var source = TestRazorSourceDocument.Create(content, filePath: null);
        var codeDocument = ProjectEngine.CreateCodeDocument(source);
        var originalTree = RazorSyntaxTree.Parse(source);
        codeDocument.SetSyntaxTree(originalTree);

        // Act
        ProjectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);

        // Assert
        var rewrittenTree = codeDocument.GetSyntaxTree();
        var erroredNode = rewrittenTree.Root.DescendantNodes().First(n => n.GetChunkGenerator() is RemoveTagHelperChunkGenerator);
        var chunkGenerator = Assert.IsType<RemoveTagHelperChunkGenerator>(erroredNode.GetChunkGenerator());
        Assert.Equal(expectedDiagnostics, chunkGenerator.Diagnostics);
    }

    [Fact]
    public void Execute_CanHandleSingleLengthTagHelperPrefix()
    {
        // Arrange
        var expectedDiagnostics = new[]
        {
            RazorDiagnosticFactory.CreateParsing_UnterminatedStringLiteral(
                new SourceSpan(new SourceLocation(17 + Environment.NewLine.Length, 1, 17), contentLength: 1)),
            RazorDiagnosticFactory.CreateParsing_InvalidTagHelperPrefixValue(
                new SourceSpan(new SourceLocation(17 + Environment.NewLine.Length, 1, 17), contentLength: 1), "tagHelperPrefix", '\"', "\""),
        };

        var content =
        @"
@tagHelperPrefix """;
        var source = TestRazorSourceDocument.Create(content, filePath: null);
        var codeDocument = ProjectEngine.CreateCodeDocument(source);
        var originalTree = RazorSyntaxTree.Parse(source);
        codeDocument.SetSyntaxTree(originalTree);

        // Act
        ProjectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);

        // Assert
        var rewrittenTree = codeDocument.GetSyntaxTree();
        var erroredNode = rewrittenTree.Root.DescendantNodes().First(n => n.GetChunkGenerator() is TagHelperPrefixDirectiveChunkGenerator);
        var chunkGenerator = Assert.IsType<TagHelperPrefixDirectiveChunkGenerator>(erroredNode.GetChunkGenerator());
        Assert.Equal(expectedDiagnostics, chunkGenerator.Diagnostics);
    }

    [Fact]
    public void Execute_RewritesTagHelpers()
    {
        // Arrange
        var tagHelper1 = CreateTagHelperDescriptor(
            tagName: "form",
            typeName: "TestFormTagHelper",
            assemblyName: "TestAssembly");

        var tagHelper2 = CreateTagHelperDescriptor(
            tagName: "input",
            typeName: "TestInputTagHelper",
            assemblyName: "TestAssembly");

        var projectEngine = RazorProjectEngine.Create(builder =>
        {
            builder.AddTagHelpers(tagHelper1, tagHelper2);
        });

        var source = CreateTestSourceDocument();
        var codeDocument = projectEngine.CreateCodeDocument(source);
        var originalTree = RazorSyntaxTree.Parse(source);
        codeDocument.SetSyntaxTree(originalTree);

        // Act
        projectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);
        projectEngine.ExecutePhase<DefaultRazorTagHelperRewritePhase>(codeDocument);

        // Assert
        var rewrittenTree = codeDocument.GetSyntaxTree();
        Assert.Empty(rewrittenTree.Diagnostics);

        var tagHelperNodes = rewrittenTree.Root.DescendantNodes().OfType<MarkupTagHelperElementSyntax>().ToArray();
        Assert.Equal("form", tagHelperNodes[0].TagHelperInfo.TagName);
        Assert.Equal("input", tagHelperNodes[1].TagHelperInfo.TagName);
    }

    [Fact]
    public void Execute_WithTagHelperDescriptorsFromCodeDocument_RewritesTagHelpers()
    {
        // Arrange
        var tagHelper1 = CreateTagHelperDescriptor(
            tagName: "form",
            typeName: "TestFormTagHelper",
            assemblyName: "TestAssembly");

        var tagHelper2 = CreateTagHelperDescriptor(
            tagName: "input",
            typeName: "TestInputTagHelper",
            assemblyName: "TestAssembly");

        var sourceDocument = CreateTestSourceDocument();
        var codeDocument = ProjectEngine.CreateCodeDocument(sourceDocument);
        var originalTree = RazorSyntaxTree.Parse(sourceDocument);
        codeDocument.SetSyntaxTree(originalTree);
        codeDocument.SetTagHelpers([tagHelper1, tagHelper2]);

        // Act
        ProjectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);
        ProjectEngine.ExecutePhase<DefaultRazorTagHelperRewritePhase>(codeDocument);

        // Assert
        var rewrittenTree = codeDocument.GetSyntaxTree();
        Assert.Empty(rewrittenTree.Diagnostics);

        var tagHelperNodes = rewrittenTree.Root.DescendantNodes().OfType<MarkupTagHelperElementSyntax>().ToArray();
        Assert.Equal("form", tagHelperNodes[0].TagHelperInfo.TagName);
        Assert.Equal("input", tagHelperNodes[1].TagHelperInfo.TagName);
    }

    [Fact]
    public void Execute_NullTagHelperDescriptorsFromCodeDocument_FallsBackToTagHelperFeature()
    {
        // Arrange
        var tagHelper1 = CreateTagHelperDescriptor(
            tagName: "form",
            typeName: "TestFormTagHelper",
            assemblyName: "TestAssembly");

        var tagHelper2 = CreateTagHelperDescriptor(
            tagName: "input",
            typeName: "TestInputTagHelper",
            assemblyName: "TestAssembly");

        var projectEngine = RazorProjectEngine.Create(builder =>
        {
            builder.AddTagHelpers(tagHelper1, tagHelper2);
        });

        var source = CreateTestSourceDocument();
        var codeDocument = projectEngine.CreateCodeDocument(source);
        var originalTree = RazorSyntaxTree.Parse(source);
        codeDocument.SetSyntaxTree(originalTree);
        codeDocument.SetTagHelpers(value: null);

        // Act
        projectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);
        projectEngine.ExecutePhase<DefaultRazorTagHelperRewritePhase>(codeDocument);

        // Assert
        var rewrittenTree = codeDocument.GetSyntaxTree();
        Assert.Empty(rewrittenTree.Diagnostics);

        var tagHelperNodes = rewrittenTree.Root.DescendantNodes().OfType<MarkupTagHelperElementSyntax>().ToArray();
        Assert.Equal("form", tagHelperNodes[0].TagHelperInfo.TagName);
        Assert.Equal("input", tagHelperNodes[1].TagHelperInfo.TagName);
    }

    [Fact]
    public void Execute_EmptyTagHelperDescriptorsFromCodeDocument_DoesNotFallbackToTagHelperFeature()
    {
        // Arrange
        var tagHelper1 = CreateTagHelperDescriptor(
            tagName: "form",
            typeName: "TestFormTagHelper",
            assemblyName: "TestAssembly");

        var tagHelper2 = CreateTagHelperDescriptor(
            tagName: "input",
            typeName: "TestInputTagHelper",
            assemblyName: "TestAssembly");

        var projectEngine = RazorProjectEngine.Create(builder =>
        {
            builder.AddTagHelpers(tagHelper1, tagHelper2);
        });

        var source = CreateTestSourceDocument();
        var codeDocument = projectEngine.CreateCodeDocument(source);
        var originalTree = RazorSyntaxTree.Parse(source);
        codeDocument.SetSyntaxTree(originalTree);
        codeDocument.SetTagHelpers(value: []);

        // Act
        projectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);

        // Assert
        var rewrittenTree = codeDocument.GetSyntaxTree();
        Assert.Empty(rewrittenTree.Diagnostics);

        var tagHelperNodes = rewrittenTree.Root.DescendantNodes().OfType<MarkupTagHelperElementSyntax>().ToArray();
        Assert.Empty(tagHelperNodes);
    }

    [Fact]
    public void Execute_DirectiveWithoutQuotes_RewritesTagHelpers_TagHelperMatchesElementTwice()
    {
        // Arrange
        var tagHelper = CreateTagHelperDescriptor(
            tagName: "form",
            typeName: "TestFormTagHelper",
            assemblyName: "TestAssembly",
            ruleBuilders:
            [
                ruleBuilder => ruleBuilder
                    .RequireAttributeDescriptor(attribute => attribute
                        .Name("a", RequiredAttributeNameComparison.FullMatch)),
                ruleBuilder => ruleBuilder
                    .RequireAttributeDescriptor(attribute => attribute
                        .Name("b", RequiredAttributeNameComparison.FullMatch)),
            ]);

        var content = @"
@addTagHelper *, TestAssembly
<form a=""hi"" b=""there"">
</form>";

        var source = TestRazorSourceDocument.Create(content);
        var codeDocument = ProjectEngine.CreateCodeDocument(source, [tagHelper]);
        var originalTree = RazorSyntaxTree.Parse(source);
        codeDocument.SetSyntaxTree(originalTree);

        // Act
        ProjectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);
        ProjectEngine.ExecutePhase<DefaultRazorTagHelperRewritePhase>(codeDocument);

        // Assert
        var rewrittenTree = codeDocument.GetSyntaxTree();
        Assert.Empty(rewrittenTree.Diagnostics);

        var tagHelperNodes = rewrittenTree.Root.DescendantNodes().OfType<MarkupTagHelperElementSyntax>().ToArray();
        var formTagHelper = Assert.Single(tagHelperNodes);
        Assert.Equal("form", formTagHelper.TagHelperInfo.TagName);
        Assert.Equal(2, formTagHelper.TagHelperInfo.BindingResult.GetBoundRules(tagHelper).Length);
    }

    [Fact]
    public void Execute_DirectiveWithQuotes_RewritesTagHelpers_TagHelperMatchesElementTwice()
    {
        // Arrange
        var tagHelper = CreateTagHelperDescriptor(
            tagName: "form",
            typeName: "TestFormTagHelper",
            assemblyName: "TestAssembly",
            ruleBuilders:
            [
                ruleBuilder => ruleBuilder
                    .RequireAttributeDescriptor(attribute => attribute
                        .Name("a", RequiredAttributeNameComparison.FullMatch)),
                ruleBuilder => ruleBuilder
                    .RequireAttributeDescriptor(attribute => attribute
                        .Name("b", RequiredAttributeNameComparison.FullMatch)),
            ]);

        var content = @"
@addTagHelper ""*, TestAssembly""
<form a=""hi"" b=""there"">
</form>";

        var source = TestRazorSourceDocument.Create(content);
        var codeDocument = ProjectEngine.CreateCodeDocument(source, [tagHelper]);
        var originalTree = RazorSyntaxTree.Parse(source);
        codeDocument.SetSyntaxTree(originalTree);

        // Act
        ProjectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);
        ProjectEngine.ExecutePhase<DefaultRazorTagHelperRewritePhase>(codeDocument);

        // Assert
        var rewrittenTree = codeDocument.GetSyntaxTree();
        Assert.Empty(rewrittenTree.Diagnostics);

        var tagHelperNodes = rewrittenTree.Root.DescendantNodes().OfType<MarkupTagHelperElementSyntax>().ToArray();
        var formTagHelper = Assert.Single(tagHelperNodes);
        Assert.Equal("form", formTagHelper.TagHelperInfo.TagName);
        Assert.Equal(2, formTagHelper.TagHelperInfo.BindingResult.GetBoundRules(tagHelper).Length);
    }

    [Fact]
    public void Execute_TagHelpersFromCodeDocumentAndFeature_PrefersCodeDocument()
    {
        // Arrange
        var featureTagHelper = CreateTagHelperDescriptor(
            tagName: "input",
            typeName: "TestInputTagHelper",
            assemblyName: "TestAssembly");

        var projectEngine = RazorProjectEngine.Create(builder =>
        {
            builder.AddTagHelpers(featureTagHelper);
        });

        var source = CreateTestSourceDocument();
        var codeDocument = ProjectEngine.CreateCodeDocument(source);
        var originalTree = RazorSyntaxTree.Parse(source);
        codeDocument.SetSyntaxTree(originalTree);

        var codeDocumentTagHelper = CreateTagHelperDescriptor(
            tagName: "form",
            typeName: "TestFormTagHelper",
            assemblyName: "TestAssembly");

        codeDocument.SetTagHelpers([codeDocumentTagHelper]);

        // Act
        ProjectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);
        ProjectEngine.ExecutePhase<DefaultRazorTagHelperRewritePhase>(codeDocument);

        // Assert
        var rewrittenTree = codeDocument.GetSyntaxTree();
        Assert.Empty(rewrittenTree.Diagnostics);

        var tagHelperNodes = rewrittenTree.Root.DescendantNodes().OfType<MarkupTagHelperElementSyntax>().ToArray();
        var formTagHelper = Assert.Single(tagHelperNodes);
        Assert.Equal("form", formTagHelper.TagHelperInfo.TagName);
    }

    [Fact]
    public void Execute_NoopsWhenNoTagHelpersFromCodeDocumentOrFeature()
    {
        // Arrange
        var source = CreateTestSourceDocument();
        var codeDocument = ProjectEngine.CreateCodeDocument(source);
        var originalTree = RazorSyntaxTree.Parse(source);
        codeDocument.SetSyntaxTree(originalTree);

        // Act
        ProjectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);

        // Assert
        var outputTree = codeDocument.GetSyntaxTree();
        Assert.Empty(outputTree.Diagnostics);
        Assert.Same(originalTree, outputTree);
    }

    [Fact]
    public void Execute_NoopsWhenNoTagHelperDescriptorsAreResolved()
    {
        // Arrange

        // No taghelper directives here so nothing is resolved.
        var source = TestRazorSourceDocument.Create("Hello, world");
        var codeDocument = ProjectEngine.CreateCodeDocument(source);
        var originalTree = RazorSyntaxTree.Parse(source);
        codeDocument.SetSyntaxTree(originalTree);

        // Act
        ProjectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);

        // Assert
        var outputTree = codeDocument.GetSyntaxTree();
        Assert.Empty(outputTree.Diagnostics);
        Assert.Same(originalTree, outputTree);
    }

    [Fact]
    public void Execute_SetsTagHelperDocumentContext()
    {
        // Arrange
        var projectEngine = RazorProjectEngine.Create(builder =>
        {
            builder.Features.Add(new TestTagHelperFeature());
        });

        // No taghelper directives here so nothing is resolved.
        var source = TestRazorSourceDocument.Create("Hello, world");
        var codeDocument = projectEngine.CreateCodeDocument(source);
        var originalTree = RazorSyntaxTree.Parse(source);
        codeDocument.SetSyntaxTree(originalTree);

        // Act
        projectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);

        // Assert
        var context = codeDocument.GetTagHelperContext();
        Assert.NotNull(context);
        Assert.Null(context.Prefix);
        Assert.Empty(context.TagHelpers);
    }

    [Fact]
    public void Execute_CombinesErrorsOnRewritingErrors()
    {
        // Arrange
        var tagHelper1 = CreateTagHelperDescriptor(
            tagName: "form",
            typeName: "TestFormTagHelper",
            assemblyName: "TestAssembly");

        var tagHelper2 = CreateTagHelperDescriptor(
            tagName: "input",
            typeName: "TestInputTagHelper",
            assemblyName: "TestAssembly");

        var projectEngine = RazorProjectEngine.Create(builder =>
        {
            builder.AddTagHelpers(tagHelper1, tagHelper2);
        });

        var content =
        @"
@addTagHelper *, TestAssembly
<form>
    <input value='Hello' type='text' />";
        var source = TestRazorSourceDocument.Create(content, filePath: null);
        var codeDocument = projectEngine.CreateCodeDocument(source);

        var originalTree = RazorSyntaxTree.Parse(source);

        var initialError = RazorDiagnostic.Create(
            new RazorDiagnosticDescriptor("RZ9999", "Initial test error", RazorDiagnosticSeverity.Error),
            new SourceSpan(SourceLocation.Zero, contentLength: 1));
        var expectedRewritingError = RazorDiagnosticFactory.CreateParsing_TagHelperFoundMalformedTagHelper(
            new SourceSpan(new SourceLocation((Environment.NewLine.Length * 2) + 30, 2, 1), contentLength: 4), "form");

        var erroredOriginalTree = new RazorSyntaxTree(originalTree.Root, originalTree.Source, [initialError], originalTree.Options);
        codeDocument.SetSyntaxTree(erroredOriginalTree);

        // Act
        projectEngine.ExecutePhase<DefaultRazorTagHelperContextDiscoveryPhase>(codeDocument);
        projectEngine.ExecutePhase<DefaultRazorTagHelperRewritePhase>(codeDocument);

        // Assert
        var outputTree = codeDocument.GetSyntaxTree();
        Assert.Empty(originalTree.Diagnostics);
        Assert.NotSame(erroredOriginalTree, outputTree);
        Assert.Equal<RazorDiagnostic>([initialError, expectedRewritingError], outputTree.Diagnostics);
    }

    private static string AssemblyA => "TestAssembly";

    private static string AssemblyB => "AnotherAssembly";

    private static TagHelperDescriptor Valid_PlainTagHelperDescriptor
    {
        get
        {
            return CreateTagHelperDescriptor(
                tagName: "valid_plain",
                typeName: "Microsoft.AspNetCore.Razor.TagHelpers.ValidPlainTagHelper",
                assemblyName: AssemblyA);
        }
    }

    private static TagHelperDescriptor Valid_InheritedTagHelperDescriptor
    {
        get
        {
            return CreateTagHelperDescriptor(
                tagName: "valid_inherited",
                typeName: "Microsoft.AspNetCore.Razor.TagHelpers.ValidInheritedTagHelper",
                assemblyName: AssemblyA);
        }
    }

    private static TagHelperDescriptor String_TagHelperDescriptor
    {
        get
        {
            // We're treating 'string' as a TagHelper so we can test TagHelpers in multiple assemblies without
            // building a separate assembly with a single TagHelper.
            return CreateTagHelperDescriptor(
                tagName: "string",
                typeName: "System.String",
                assemblyName: AssemblyB);
        }
    }

    public static TheoryData<string, string> ProcessTagHelperPrefixData
    {
        get
        {
            // source, expected prefix
            return new TheoryData<string, string>
            {
                {
                    $@"
@tagHelperPrefix """"
@addTagHelper Microsoft.AspNetCore.Razor.TagHelpers.ValidPlain*, TestAssembly",
                    null
                },
                {
                    $@"
@tagHelperPrefix th:
@addTagHelper Microsoft.AspNetCore.Razor.TagHelpers.ValidPlain*, {AssemblyA}",
                    "th:"
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@tagHelperPrefix th:",
                    "th:"
                },
                {
                    $@"
@tagHelperPrefix th-
@addTagHelper Microsoft.AspNetCore.Razor.TagHelpers.ValidPlain*, {AssemblyA}
@addTagHelper Microsoft.AspNetCore.Razor.TagHelpers.ValidInherited*, {AssemblyA}",
                    "th-"
                },
                {
                    $@"
@tagHelperPrefix
@addTagHelper Microsoft.AspNetCore.Razor.TagHelpers.ValidPlain*, {AssemblyA}
@addTagHelper Microsoft.AspNetCore.Razor.TagHelpers.ValidInherited*, {AssemblyA}",
                    null
                },
                {
                    $@"
@tagHelperPrefix ""th""
@addTagHelper *, {AssemblyA}
@addTagHelper *, {AssemblyB}",
                    "th"
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@tagHelperPrefix th:-
@addTagHelper *, {AssemblyB}",
                    "th:-"
                },
            };
        }
    }

    [Theory]
    [MemberData(nameof(ProcessTagHelperPrefixData))]
    public void DirectiveVisitor_ExtractsPrefixFromSyntaxTree(
        string source,
        string expectedPrefix)
    {
        // Arrange
        var sourceDocument = TestRazorSourceDocument.Create(source, filePath: "TestFile");
        var parser = new RazorParser();
        var syntaxTree = parser.Parse(sourceDocument);
        var visitor = new DefaultRazorTagHelperContextDiscoveryPhase.TagHelperDirectiveVisitor();
        visitor.Initialize(tagHelpers: []);

        // Act
        visitor.Visit(syntaxTree.Root);

        // Assert
        Assert.Equal(expectedPrefix, visitor.TagHelperPrefix);
    }

    public static TheoryData<string, TagHelperDescriptor[], TagHelperDescriptor[]> ProcessTagHelperMatchesData
    {
        get
        {
            // source, taghelpers, expected descriptors
            return new TheoryData<string, TagHelperDescriptor[], TagHelperDescriptor[]>
            {
                {
                    $@"
@addTagHelper *, {AssemblyA}",
                    new [] { Valid_PlainTagHelperDescriptor, },
                    new [] { Valid_PlainTagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@addTagHelper *, {AssemblyB}",
                    new [] { Valid_PlainTagHelperDescriptor, String_TagHelperDescriptor },
                    new [] { Valid_PlainTagHelperDescriptor, String_TagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@removeTagHelper *, {AssemblyB}",
                    new [] { Valid_PlainTagHelperDescriptor, String_TagHelperDescriptor },
                    new [] { Valid_PlainTagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@addTagHelper *, {AssemblyB}
@removeTagHelper *, {AssemblyA}",
                    new [] { Valid_PlainTagHelperDescriptor, Valid_InheritedTagHelperDescriptor, String_TagHelperDescriptor },
                    new [] { String_TagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper {Valid_PlainTagHelperDescriptor.Name}, {AssemblyA}
@addTagHelper *, {AssemblyA}",
                    new [] { Valid_PlainTagHelperDescriptor, Valid_InheritedTagHelperDescriptor, },
                    new [] { Valid_PlainTagHelperDescriptor, Valid_InheritedTagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@removeTagHelper {Valid_PlainTagHelperDescriptor.Name}, {AssemblyA}",
                    new [] { Valid_PlainTagHelperDescriptor, Valid_InheritedTagHelperDescriptor, },
                    new [] { Valid_InheritedTagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@removeTagHelper *, {AssemblyA}
@addTagHelper *, {AssemblyA}",
                    new [] { Valid_PlainTagHelperDescriptor, Valid_InheritedTagHelperDescriptor, },
                    new [] { Valid_InheritedTagHelperDescriptor, Valid_PlainTagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@addTagHelper *, {AssemblyA}",
                    new [] { Valid_PlainTagHelperDescriptor, Valid_InheritedTagHelperDescriptor, },
                    new [] { Valid_InheritedTagHelperDescriptor, Valid_PlainTagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper Microsoft.AspNetCore.Razor.TagHelpers.ValidPlain*, {AssemblyA}",
                    new [] { Valid_PlainTagHelperDescriptor, Valid_InheritedTagHelperDescriptor, },
                    new [] { Valid_PlainTagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper Microsoft.AspNetCore.Razor.TagHelpers.*, {AssemblyA}",
                    new [] { Valid_PlainTagHelperDescriptor, Valid_InheritedTagHelperDescriptor, },
                    new [] { Valid_PlainTagHelperDescriptor, Valid_PlainTagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@removeTagHelper Microsoft.AspNetCore.Razor.TagHelpers.ValidP*, {AssemblyA}
@addTagHelper *, {AssemblyA}",
                    new [] { Valid_PlainTagHelperDescriptor, Valid_InheritedTagHelperDescriptor, },
                    new [] { Valid_InheritedTagHelperDescriptor, Valid_PlainTagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@removeTagHelper Str*, {AssemblyB}",
                    new [] { Valid_PlainTagHelperDescriptor, String_TagHelperDescriptor, },
                    new [] { Valid_PlainTagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@removeTagHelper *, {AssemblyB}",
                    new [] { Valid_PlainTagHelperDescriptor, String_TagHelperDescriptor, },
                    new [] { Valid_PlainTagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@addTagHelper System.{String_TagHelperDescriptor.Name}, {AssemblyB}",
                    new [] { Valid_PlainTagHelperDescriptor, String_TagHelperDescriptor, },
                    new [] { Valid_PlainTagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@addTagHelper *, {AssemblyB}
@removeTagHelper Microsoft.*, {AssemblyA}",
                    new [] { Valid_PlainTagHelperDescriptor, Valid_InheritedTagHelperDescriptor, String_TagHelperDescriptor },
                    new [] { String_TagHelperDescriptor }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@addTagHelper *, {AssemblyB}
@removeTagHelper ?Microsoft*, {AssemblyA}
@removeTagHelper System.{String_TagHelperDescriptor.Name}, {AssemblyB}",
                    new [] { Valid_PlainTagHelperDescriptor, Valid_InheritedTagHelperDescriptor, String_TagHelperDescriptor },
                    new []
                    {
                        Valid_InheritedTagHelperDescriptor,
                        Valid_PlainTagHelperDescriptor,
                        String_TagHelperDescriptor
                    }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@addTagHelper *, {AssemblyB}
@removeTagHelper TagHelper*, {AssemblyA}
@removeTagHelper System.{String_TagHelperDescriptor.Name}, {AssemblyB}",
                    new [] { Valid_PlainTagHelperDescriptor, Valid_InheritedTagHelperDescriptor, String_TagHelperDescriptor },
                    new []
                    {
                        Valid_InheritedTagHelperDescriptor,
                        Valid_PlainTagHelperDescriptor,
                        String_TagHelperDescriptor
                    }
                },
            };
        }
    }

    [Theory]
    [MemberData(nameof(ProcessTagHelperMatchesData))]
    public void DirectiveVisitor_FiltersTagHelpersByDirectives(
        string source,
        TagHelperDescriptor[] tagHelpers,
        TagHelperDescriptor[] expectedTagHelpers)
    {
        // Arrange
        var sourceDocument = TestRazorSourceDocument.Create(source, filePath: "TestFile");
        var parser = new RazorParser();
        var syntaxTree = parser.Parse(sourceDocument);
        var visitor = new DefaultRazorTagHelperContextDiscoveryPhase.TagHelperDirectiveVisitor();
        visitor.Initialize(tagHelpers);

        // Act
        visitor.Visit(syntaxTree.Root);
        var results = visitor.GetResults();

        // Assert
        Assert.Equal(expectedTagHelpers.Length, results.Length);

        foreach (var expectedTagHelper in expectedTagHelpers)
        {
            Assert.Contains(expectedTagHelper, results);
        }
    }

    public static TheoryData<string, IReadOnlyList<TagHelperDescriptor>> ProcessTagHelperMatches_EmptyResultData
    {
        get
        {
            // source, taghelpers
            return new TheoryData<string, IReadOnlyList<TagHelperDescriptor>>
            {
                {
                    $@"
@addTagHelper *, {AssemblyA}
@removeTagHelper *, {AssemblyA}",
                    new TagHelperDescriptor[]
                    {
                        Valid_PlainTagHelperDescriptor,
                    }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@removeTagHelper {Valid_PlainTagHelperDescriptor.Name}, {AssemblyA}
@removeTagHelper {Valid_InheritedTagHelperDescriptor.Name}, {AssemblyA}",
                    new TagHelperDescriptor[]
                    {
                        Valid_PlainTagHelperDescriptor,
                        Valid_InheritedTagHelperDescriptor,
                    }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@addTagHelper *, {AssemblyB}
@removeTagHelper *, {AssemblyA}
@removeTagHelper *, {AssemblyB}",
                    new TagHelperDescriptor[]
                    {
                        Valid_PlainTagHelperDescriptor,
                        Valid_InheritedTagHelperDescriptor,
                        String_TagHelperDescriptor,
                    }
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@addTagHelper *, {AssemblyB}
@removeTagHelper {Valid_PlainTagHelperDescriptor.Name}, {AssemblyA}
@removeTagHelper {Valid_InheritedTagHelperDescriptor.Name}, {AssemblyA}
@removeTagHelper {String_TagHelperDescriptor.Name}, {AssemblyB}",
                    new TagHelperDescriptor[]
                    {
                        Valid_PlainTagHelperDescriptor,
                        Valid_InheritedTagHelperDescriptor,
                        String_TagHelperDescriptor,
                    }
                },
                {
                    $@"
@removeTagHelper *, {AssemblyA}
@removeTagHelper {Valid_PlainTagHelperDescriptor.Name}, {AssemblyA}",
                    new TagHelperDescriptor[0]
                },
                {
                    $@"
@addTagHelper *, {AssemblyA}
@removeTagHelper Mic*, {AssemblyA}",
                    new TagHelperDescriptor[]
                    {
                        Valid_PlainTagHelperDescriptor,
                    }
                },
                {
                    $@"
@addTagHelper Mic*, {AssemblyA}
@removeTagHelper {Valid_PlainTagHelperDescriptor.Name}, {AssemblyA}
@removeTagHelper {Valid_InheritedTagHelperDescriptor.Name}, {AssemblyA}",
                    new TagHelperDescriptor[]
                    {
                        Valid_PlainTagHelperDescriptor, Valid_InheritedTagHelperDescriptor
                    }
                },
                {
                    $@"
@addTagHelper Microsoft.*, {AssemblyA}
@addTagHelper System.*, {AssemblyB}
@removeTagHelper Microsoft.AspNetCore.Razor.TagHelpers*, {AssemblyA}
@removeTagHelper System.*, {AssemblyB}",
                    new TagHelperDescriptor[]
                    {
                        Valid_PlainTagHelperDescriptor,
                        Valid_InheritedTagHelperDescriptor,
                        String_TagHelperDescriptor,
                    }
                },
                {
                    $@"
@addTagHelper ?icrosoft.*, {AssemblyA}
@addTagHelper ?ystem.*, {AssemblyB}
@removeTagHelper *?????r, {AssemblyA}
@removeTagHelper Sy??em.*, {AssemblyB}",
                    new TagHelperDescriptor[]
                    {
                        Valid_PlainTagHelperDescriptor,
                        Valid_InheritedTagHelperDescriptor,
                        String_TagHelperDescriptor,
                    }
                },
                {
                    $@"
@addTagHelper ?i?crosoft.*, {AssemblyA}
@addTagHelper ??ystem.*, {AssemblyB}",
                    new TagHelperDescriptor[]
                    {
                        Valid_PlainTagHelperDescriptor,
                        Valid_InheritedTagHelperDescriptor,
                        String_TagHelperDescriptor,
                    }
                },
            };
        }
    }

    [Theory]
    [MemberData(nameof(ProcessTagHelperMatches_EmptyResultData))]
    public void ProcessDirectives_CanReturnEmptyDescriptorsBasedOnDirectiveDescriptors(
        string source,
        IReadOnlyList<TagHelperDescriptor> tagHelpers)
    {
        // Arrange
        var sourceDocument = TestRazorSourceDocument.Create(source, filePath: "TestFile");
        var parser = new RazorParser();
        var syntaxTree = parser.Parse(sourceDocument);
        var visitor = new DefaultRazorTagHelperContextDiscoveryPhase.TagHelperDirectiveVisitor();
        visitor.Initialize(tagHelpers);

        // Act
        visitor.Visit(syntaxTree.Root);
        var results = visitor.GetResults();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void TagHelperDirectiveVisitor_DoesNotMatch_Components()
    {
        // Arrange
        var componentDescriptor = CreateComponentDescriptor("counter", "SomeProject.Counter", AssemblyA);
        var legacyDescriptor = Valid_PlainTagHelperDescriptor;
        var tagHelpers = new[]
        {
            legacyDescriptor,
            componentDescriptor,
        };

        var visitor = new DefaultRazorTagHelperContextDiscoveryPhase.TagHelperDirectiveVisitor();
        visitor.Initialize(tagHelpers);
        var sourceDocument = CreateTestSourceDocument();
        var tree = RazorSyntaxTree.Parse(sourceDocument);

        // Act
        visitor.Visit(tree);
        var results = visitor.GetResults();

        // Assert
        var result = Assert.Single(results);
        Assert.Same(legacyDescriptor, result);
    }

    private static RazorSourceDocument CreateTestSourceDocument()
    {
        var content =
        @"
@addTagHelper *, TestAssembly
<form>
    <input value='Hello' type='text' />
</form>";

        return TestRazorSourceDocument.Create(content, filePath: null);
    }

    private static TagHelperDescriptor CreateTagHelperDescriptor(
        string tagName,
        string typeName,
        string assemblyName,
        string typeNamespace = null,
        string typeNameIdentifier = null,
        IEnumerable<Action<BoundAttributeDescriptorBuilder>> attributes = null,
        IEnumerable<Action<TagMatchingRuleDescriptorBuilder>> ruleBuilders = null)
    {
        return CreateDescriptor(TagHelperConventions.DefaultKind, tagName, typeName, assemblyName, typeNamespace, typeNameIdentifier, attributes, ruleBuilders);
    }
    #endregion

    #region Components
    [Fact]
    public void ComponentDirectiveVisitor_DoesNotMatch_LegacyTagHelpers()
    {
        // Arrange
        var currentNamespace = "SomeProject";
        var componentDescriptor = CreateComponentDescriptor("counter", "SomeProject.Counter", AssemblyA);
        var legacyDescriptor = Valid_PlainTagHelperDescriptor;
        var descriptors = new[]
        {
            legacyDescriptor,
            componentDescriptor,
        };
        var sourceDocument = CreateComponentTestSourceDocument(@"<Counter />", "C:\\SomeFolder\\SomeProject\\Counter.cshtml");
        var tree = RazorSyntaxTree.Parse(sourceDocument);
        var visitor = new DefaultRazorTagHelperContextDiscoveryPhase.ComponentDirectiveVisitor();
        visitor.Initialize(sourceDocument.FilePath, descriptors, currentNamespace);

        // Act
        visitor.Visit(tree);

        // Assert
        Assert.Null(visitor.TagHelperPrefix);
        var result = Assert.Single(visitor.GetResults());
        Assert.Same(componentDescriptor, result);
    }

    [Fact]
    public void ComponentDirectiveVisitor_AddsErrorOnLegacyTagHelperDirectives()
    {
        // Arrange
        var currentNamespace = "SomeProject";
        var componentDescriptor = CreateComponentDescriptor("counter", "SomeProject.Counter", AssemblyA);
        var legacyDescriptor = Valid_PlainTagHelperDescriptor;
        var descriptors = new[]
        {
            legacyDescriptor,
            componentDescriptor,
        };
        var filePath = "C:\\SomeFolder\\SomeProject\\Counter.cshtml";
        var content = @"
@tagHelperPrefix th:

<Counter />
";
        var sourceDocument = CreateComponentTestSourceDocument(content, filePath);
        var tree = RazorSyntaxTree.Parse(sourceDocument);
        var visitor = new DefaultRazorTagHelperContextDiscoveryPhase.ComponentDirectiveVisitor();
        visitor.Initialize(sourceDocument.FilePath, descriptors, currentNamespace);

        // Act
        visitor.Visit(tree);
        var results = visitor.GetResults();

        // Assert
        Assert.Null(visitor.TagHelperPrefix);
        var result = Assert.Single(results);
        Assert.Same(componentDescriptor, result);
        var directiveChunkGenerator = (TagHelperPrefixDirectiveChunkGenerator)tree.Root.DescendantNodes().First(n => n is CSharpStatementLiteralSyntax).GetChunkGenerator();
        var diagnostic = Assert.Single(directiveChunkGenerator.Diagnostics);
        Assert.Equal("RZ9978", diagnostic.Id);
    }

    [Fact]
    public void ComponentDirectiveVisitor_MatchesFullyQualifiedComponents()
    {
        // Arrange
        var currentNamespace = "SomeProject";
        var componentDescriptor = CreateComponentDescriptor(
            "SomeProject.SomeOtherFolder.Counter",
            "SomeProject.SomeOtherFolder.Counter",
            AssemblyA,
            fullyQualified: true);
        var descriptors = new[]
        {
            componentDescriptor,
        };
        var filePath = "C:\\SomeFolder\\SomeProject\\Counter.cshtml";
        var content = @"
";
        var sourceDocument = CreateComponentTestSourceDocument(content, filePath);
        var tree = RazorSyntaxTree.Parse(sourceDocument);
        var visitor = new DefaultRazorTagHelperContextDiscoveryPhase.ComponentDirectiveVisitor();
        visitor.Initialize(sourceDocument.FilePath, descriptors, currentNamespace);

        // Act
        visitor.Visit(tree);
        var results = visitor.GetResults();

        // Assert
        var result = Assert.Single(results);
        Assert.Same(componentDescriptor, result);
    }

    [Fact]
    public void ComponentDirectiveVisitor_ComponentInScope_MatchesChildContent()
    {
        // Arrange
        var currentNamespace = "SomeProject";
        var componentDescriptor = CreateComponentDescriptor(
            "Counter",
            "SomeProject.Counter",
            AssemblyA);
        var childContentDescriptor = CreateComponentDescriptor(
            "ChildContent",
            "SomeProject.Counter.ChildContent",
            AssemblyA,
            "SomeProject",
            "Counter",
            childContent: true);
        var descriptors = new[]
        {
            componentDescriptor,
            childContentDescriptor,
        };
        var filePath = "C:\\SomeFolder\\SomeProject\\Counter.cshtml";
        var content = @"
";
        var sourceDocument = CreateComponentTestSourceDocument(content, filePath);
        var tree = RazorSyntaxTree.Parse(sourceDocument);
        var visitor = new DefaultRazorTagHelperContextDiscoveryPhase.ComponentDirectiveVisitor();
        visitor.Initialize(sourceDocument.FilePath, descriptors, currentNamespace);

        // Act
        visitor.Visit(tree);
        var results = visitor.GetResults();

        // Assert
        Assert.Equal(2, results.Length);
    }

    [Fact]
    public void ComponentDirectiveVisitor_NullCurrentNamespace_MatchesOnlyFullyQualifiedComponents()
    {
        // Arrange
        string currentNamespace = null;
        var componentDescriptor = CreateComponentDescriptor(
            "Counter",
            "SomeProject.Counter",
            AssemblyA);
        var fullyQualifiedComponent = CreateComponentDescriptor(
           "SomeProject.SomeOtherFolder.Counter",
           "SomeProject.SomeOtherFolder.Counter",
           AssemblyA,
           fullyQualified: true);
        var descriptors = new[]
        {
            componentDescriptor,
            fullyQualifiedComponent,
        };
        var filePath = "C:\\SomeFolder\\SomeProject\\Counter.cshtml";
        var content = @"
";
        var sourceDocument = CreateComponentTestSourceDocument(content, filePath);
        var tree = RazorSyntaxTree.Parse(sourceDocument);
        var visitor = new DefaultRazorTagHelperContextDiscoveryPhase.ComponentDirectiveVisitor();
        visitor.Initialize(sourceDocument.FilePath, descriptors, currentNamespace);

        // Act
        visitor.Visit(tree);
        var results = visitor.GetResults();

        // Assert
        var result = Assert.Single(results);
        Assert.Same(fullyQualifiedComponent, result);
    }

    [Fact]
    public void ComponentDirectiveVisitor_MatchesIfNamespaceInUsing()
    {
        // Arrange
        var currentNamespace = "SomeProject";
        var componentDescriptor = CreateComponentDescriptor(
            "Counter",
            "SomeProject.Counter",
            AssemblyA);
        var anotherComponentDescriptor = CreateComponentDescriptor(
           "Foo",
           "SomeProject.SomeOtherFolder.Foo",
           AssemblyA);
        var descriptors = new[]
        {
            componentDescriptor,
            anotherComponentDescriptor,
        };
        var filePath = "C:\\SomeFolder\\SomeProject\\Counter.cshtml";
        var content = @"
@using SomeProject.SomeOtherFolder
";
        var sourceDocument = CreateComponentTestSourceDocument(content, filePath);
        var tree = RazorSyntaxTree.Parse(sourceDocument);
        var visitor = new DefaultRazorTagHelperContextDiscoveryPhase.ComponentDirectiveVisitor();
        visitor.Initialize(sourceDocument.FilePath, descriptors, currentNamespace);

        // Act
        visitor.Visit(tree);
        var results = visitor.GetResults();

        // Assert
        Assert.Equal(2, results.Length);
    }

    [Fact]
    public void ComponentDirectiveVisitor_MatchesIfNamespaceInUsing_GlobalPrefix()
    {
        // Arrange
        var currentNamespace = "SomeProject";
        var componentDescriptor = CreateComponentDescriptor(
            "Counter",
            "SomeProject.SomeOtherFolder.Counter",
            AssemblyA);
        var descriptors = new[]
        {
            componentDescriptor,
        };
        var filePath = "C:\\SomeFolder\\SomeProject\\Counter.cshtml";
        var content = """
            @using global::SomeProject.SomeOtherFolder
            """;
        var sourceDocument = CreateComponentTestSourceDocument(content, filePath);
        var tree = RazorSyntaxTree.Parse(sourceDocument);
        var visitor = new DefaultRazorTagHelperContextDiscoveryPhase.ComponentDirectiveVisitor();
        visitor.Initialize(sourceDocument.FilePath, descriptors, currentNamespace);

        // Act
        visitor.Visit(tree);
        var results = visitor.GetResults();

        // Assert
        var result = Assert.Single(results);
        Assert.Same(componentDescriptor, result);
    }

    [Fact]
    public void ComponentDirectiveVisitor_DoesNotMatchForUsingAliasAndStaticUsings()
    {
        // Arrange
        var currentNamespace = "SomeProject";
        var componentDescriptor = CreateComponentDescriptor(
            "Counter",
            "SomeProject.Counter",
            AssemblyA);
        var anotherComponentDescriptor = CreateComponentDescriptor(
           "Foo",
           "SomeProject.SomeOtherFolder.Foo",
           AssemblyA);
        var descriptors = new[]
        {
                componentDescriptor,
                anotherComponentDescriptor,
            };
        var filePath = "C:\\SomeFolder\\SomeProject\\Counter.cshtml";
        var content = @"
@using Bar = SomeProject.SomeOtherFolder
@using static SomeProject.SomeOtherFolder.Foo
";
        var sourceDocument = CreateComponentTestSourceDocument(content, filePath);
        var tree = RazorSyntaxTree.Parse(sourceDocument);
        var visitor = new DefaultRazorTagHelperContextDiscoveryPhase.ComponentDirectiveVisitor();
        visitor.Initialize(sourceDocument.FilePath, descriptors, currentNamespace);

        // Act
        visitor.Visit(tree);
        var results = visitor.GetResults();

        // Assert
        var result = Assert.Single(results);
        Assert.Same(componentDescriptor, result);
    }

    [Theory]
    [InlineData("", "", true)]
    [InlineData("Foo", "Project", true)]
    [InlineData("Project.Foo", "Project", true)]
    [InlineData("Project.Bar.Foo", "Project.Bar", true)]
    [InlineData("Project.Foo", "Project.Bar", true)]
    [InlineData("Project.Bar.Foo", "Project", false)]
    [InlineData("Bar.Foo", "Project", false)]
    public void IsTypeNamespaceInScope_WorksAsExpected(string typeName, string currentNamespace, bool expected)
    {
        // Arrange & Act
        var descriptor = CreateComponentDescriptor(typeName, typeName, "Test.dll");
        var tagHelperTypeNamespace = descriptor.GetTypeNamespace();

        var result = DefaultRazorTagHelperContextDiscoveryPhase.ComponentDirectiveVisitor.IsTypeNamespaceInScope(tagHelperTypeNamespace, currentNamespace);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsTagHelperFromMangledClass_WorksAsExpected()
    {
        // Arrange
        var className = "Counter";
        var typeName = $"SomeProject.SomeNamespace.{ComponentMetadata.MangleClassName(className)}";
        var descriptor = CreateComponentDescriptor(
            tagName: "Counter",
            typeName: typeName,
            assemblyName: AssemblyA);

        // Act
        var result = DefaultRazorTagHelperContextDiscoveryPhase.ComponentDirectiveVisitor.IsTagHelperFromMangledClass(descriptor);

        // Assert
        Assert.True(result);
    }

    private static RazorSourceDocument CreateComponentTestSourceDocument(string content, string filePath = null)
    {
        var sourceDocument = TestRazorSourceDocument.Create(content, filePath: filePath);
        return sourceDocument;
    }

    private static TagHelperDescriptor CreateComponentDescriptor(
        string tagName,
        string typeName,
        string assemblyName,
        string typeNamespace = null,
        string typeNameIdentifier = null,
        IEnumerable<Action<BoundAttributeDescriptorBuilder>> attributes = null,
        IEnumerable<Action<TagMatchingRuleDescriptorBuilder>> ruleBuilders = null,
        string kind = null,
        bool fullyQualified = false,
        bool childContent = false)
    {
        kind ??= ComponentMetadata.Component.TagHelperKind;
        return CreateDescriptor(kind, tagName, typeName, assemblyName, typeNamespace, typeNameIdentifier, attributes, ruleBuilders, fullyQualified, childContent);
    }
    #endregion

    private static TagHelperDescriptor CreateDescriptor(
        string kind,
        string tagName,
        string typeName,
        string assemblyName,
        string typeNamespace,
        string typeNameIdentifier,
        IEnumerable<Action<BoundAttributeDescriptorBuilder>> attributes = null,
        IEnumerable<Action<TagMatchingRuleDescriptorBuilder>> ruleBuilders = null,
        bool componentFullyQualified = false,
        bool componentChildContent = false)
    {
        var builder = TagHelperDescriptorBuilder.Create(kind, typeName, assemblyName);
        using var metadata = builder.GetMetadataBuilder();

        metadata.Add(TypeName(typeName));
        metadata.Add(TypeNamespace(typeNamespace ?? (typeName.LastIndexOf('.') == -1 ? "" : typeName[..typeName.LastIndexOf('.')])));
        metadata.Add(TypeNameIdentifier(typeNameIdentifier ?? (typeName.LastIndexOf('.') == -1 ? typeName : typeName[(typeName.LastIndexOf('.') + 1)..])));

        if (attributes != null)
        {
            foreach (var attributeBuilder in attributes)
            {
                builder.BoundAttributeDescriptor(attributeBuilder);
            }
        }

        if (ruleBuilders != null)
        {
            foreach (var ruleBuilder in ruleBuilders)
            {
                builder.TagMatchingRuleDescriptor(innerRuleBuilder =>
                {
                    innerRuleBuilder.RequireTagName(tagName);
                    ruleBuilder(innerRuleBuilder);
                });
            }
        }
        else
        {
            builder.TagMatchingRuleDescriptor(ruleBuilder => ruleBuilder.RequireTagName(tagName));
        }

        if (componentFullyQualified)
        {
            metadata.Add(ComponentMetadata.Component.NameMatchKey, ComponentMetadata.Component.FullyQualifiedNameMatch);
        }

        if (componentChildContent)
        {
            metadata.Add(SpecialKind(ComponentMetadata.ChildContent.TagHelperKind));
        }

        builder.SetMetadata(metadata.Build());

        var descriptor = builder.Build();

        return descriptor;
    }
}
