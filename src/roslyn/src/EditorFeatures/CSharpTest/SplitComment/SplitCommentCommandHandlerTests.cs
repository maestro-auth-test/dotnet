﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.Editor.UnitTests.SplitComment;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.SplitComment;

[UseExportProvider]
[Trait(Traits.Feature, Traits.Features.SplitComment)]
public sealed class SplitCommentCommandHandlerTests : AbstractSplitCommentCommandHandlerTests
{
    protected override EditorTestWorkspace CreateWorkspace(string markup)
        => EditorTestWorkspace.CreateCSharp(markup);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestWithSelection()
        => TestHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    //[|Test|] Comment
                }
            }
            """,
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    //
                    //Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestWithAllWhitespaceSelection()
        => TestHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    // [|  |] Test Comment
                }
            }
            """,
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    //
                    // Test Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestMissingInSlashes()
        => TestNotHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    /[||]/Test Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestMissingAtEndOfFile()
        => TestNotHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    //Test Comment[||]
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestMissingBeforeSlashes()
        => TestNotHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    [||]//Test Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestMissingWithMultiSelection()
        => TestNotHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    //[||]Test[||] Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestSplitStartOfComment()
        => TestHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    //[||]Test Comment
                }
            }
            """,
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    //
                    //Test Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestSplitStartOfQuadComment()
        => TestHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    ////[||]Test Comment
                }
            }
            """,
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    ////
                    ////Test Comment
                }
            }
            """);

    [WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/48547")]
    public void TestSplitMiddleOfQuadComment()
        => TestNotHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    //[||]//Test Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/48547")]
    public void TestSplitWithCommentAfterwards1()
        => TestNotHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    // goo[||]  //Test Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/48547")]
    public void TestSplitWithCommentAfterwards2()
        => TestNotHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    // goo [||] //Test Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/48547")]
    public void TestSplitWithCommentAfterwards3()
        => TestNotHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    // goo  [||]//Test Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/48547")]
    public void TestSplitWithCommentAfterwards4()
        => TestNotHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    // [|goo|] //Test Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestSplitStartOfCommentWithLeadingSpace1()
        => TestHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    // [||]Test Comment
                }
            }
            """,
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    //
                    // Test Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestSplitStartOfCommentWithLeadingSpace2()
        => TestHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    //[||] Test Comment
                }
            }
            """,
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    //
                    //Test Comment
                }
            }
            """);

    [WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    [WpfTheory]
    [InlineData("X[||]Test Comment")]
    [InlineData("X [||]Test Comment")]
    [InlineData("X[||] Test Comment")]
    [InlineData("X [||] Test Comment")]
    public void TestCommentWithMultipleLeadingSpaces(string commentValue)
        => TestHandled(
            $$"""
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    //    {{commentValue}}
                }
            }
            """,
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    //    X
                    //    Test Comment
                }
            }
            """);

    [WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    [WpfTheory]
    [InlineData("X[||]Test Comment")]
    [InlineData("X [||]Test Comment")]
    [InlineData("X[||] Test Comment")]
    [InlineData("X [||] Test Comment")]
    [InlineData("X[| |]Test Comment")]
    public void TestQuadCommentWithMultipleLeadingSpaces(string commentValue)
        => TestHandled(
            $$"""
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    ////    {{commentValue}}
                }
            }
            """,
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    ////    X
                    ////    Test Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestSplitMiddleOfComment()
        => TestHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    // Test [||]Comment
                }
            }
            """,
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    // Test
                    // Comment
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestSplitEndOfComment()
        => TestNotHandled(
            """
            public class Program
            {
                public static void Main(string[] args) 
                { 
                    // Test Comment[||]
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestSplitCommentEndOfLine1()
        => TestHandled(
            """
            public class Program
            {
                public static void Main(string[] args) // Test [||]Comment
                {
                }
            }
            """,
            """
            public class Program
            {
                public static void Main(string[] args) // Test
                                                       // Comment
                {
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestSplitCommentEndOfLine2()
        => TestHandled(
            """
            public class Program
            {
                public static void Main(string[] args) // Test[||] Comment
                {
                }
            }
            """,
            """
            public class Program
            {
                public static void Main(string[] args) // Test
                                                       // Comment
                {
                }
            }
            """);

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/38516")]
    public void TestUseTabs()
        => TestHandled(
            """
            public class Program
            {
            	public static void Main(string[] args) 
            	{
            		// X[||]Test Comment
            	}
            }
            """,
            """
            public class Program
            {
            	public static void Main(string[] args) 
            	{
            		// X
            		// Test Comment
            	}
            }
            """, useTabs: true);

    [WpfFact]
    public void TestDoesNotHandleDocComments()
        => TestNotHandled(
            """
            namespace TestNamespace
            {
                public class Program
                {
                    /// <summary>Test [||]Comment</summary>
                    public static void Main(string[] args)
                    {
                    }
                }
            }
            """);
}
