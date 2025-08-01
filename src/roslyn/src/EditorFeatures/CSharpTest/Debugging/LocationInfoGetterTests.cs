﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Debugging;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Debugging;

[UseExportProvider]
[Trait(Traits.Feature, Traits.Features.DebuggingLocationName)]
public sealed class LocationInfoGetterTests
{
    private static async Task TestAsync(string markup, string expectedName, int expectedLineOffset, CSharpParseOptions parseOptions = null)
    {
        using var workspace = EditorTestWorkspace.CreateCSharp(markup, parseOptions);

        var testDocument = workspace.Documents.Single();
        var position = testDocument.CursorPosition.Value;
        var locationInfo = await LocationInfoGetter.GetInfoAsync(
            workspace.CurrentSolution.Projects.Single().Documents.Single(),
            position,
            CancellationToken.None);

        Assert.Equal(expectedName, locationInfo.Name);
        Assert.Equal(expectedLineOffset, locationInfo.LineOffset);
    }

    [Fact]
    public async Task TestClass()
        => await TestAsync("class G$$oo { }", "Goo", 0);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/527668"), WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538415")]
    public Task TestMethod()
        => TestAsync(
            """
            class Class
            {
                public static void Meth$$od()
                {
                }
            }
            """, "Class.Method()", 0);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/527668")]
    public Task TestNamespace()
        => TestAsync(
            """
            namespace Namespace
            {
                class Class
                {
                    void Method()
                    {
                    }$$
                }
            }
            """, "Namespace.Class.Method()", 2);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/49000")]
    public Task TestFileScopedNamespace()
        => TestAsync(
            """
            namespace Namespace;

            class Class
            {
                void Method()
                {
                }$$
            }
            """, "Namespace.Class.Method()", 2);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/527668")]
    public Task TestDottedNamespace()
        => TestAsync(
            """
            namespace Namespace.Another
            {
                class Class
                {
                    void Method()
                    {
                    }$$
                }
            }
            """, "Namespace.Another.Class.Method()", 2);

    [Fact]
    public Task TestNestedNamespace()
        => TestAsync(
            """
            namespace Namespace
            {
                namespace Another
                {
                    class Class
                    {
                        void Method()
                        {
                        }$$
                    }
                }
            }
            """, "Namespace.Another.Class.Method()", 2);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/527668")]
    public Task TestNestedType()
        => TestAsync(
            """
            class Outer
            {
                class Inner
                {
                    void Quux()
                    {$$
                    }
                }
            }
            """, "Outer.Inner.Quux()", 1);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/527668")]
    public Task TestPropertyGetter()
        => TestAsync(
            """
            class Class
            {
                string Property
                {
                    get
                    {
                        return null;$$
                    }
                }
            }
            """, "Class.Property", 4);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/527668")]
    public Task TestPropertySetter()
        => TestAsync(
            """
            class Class
            {
                string Property
                {
                    get
                    {
                        return null;
                    }

                    set
                    {
                        string s = $$value;
                    }
                }
            }
            """, "Class.Property", 9);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538415")]
    public Task TestField()
        => TestAsync(
            """
            class Class
            {
                int fi$$eld;
            }
            """, "Class.field", 0);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/543494")]
    public Task TestLambdaInFieldInitializer()
        => TestAsync(
            """
            class Class
            {
                Action<int> a = b => { in$$t c; };
            }
            """, "Class.a", 0);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/543494")]
    public Task TestMultipleFields()
        => TestAsync(
            """
            class Class
            {
                int a1, a$$2;
            }
            """, "Class.a2", 0);

    [Fact]
    public Task TestConstructor()
        => TestAsync(
            """
            class C1
            {
                C1()
                {

                $$}
            }
            """, "C1.C1()", 3);

    [Fact]
    public Task TestDestructor()
        => TestAsync(
            """
            class C1
            {
                ~C1()
                {
                $$}
            }
            """, "C1.~C1()", 2);

    [Fact]
    public Task TestOperator()
        => TestAsync(
            """
            namespace N1
            {
                class C1
                {
                    public static int operator +(C1 x, C1 y)
                    {
                        $$return 42;
                    }
                }
            }
            """, "N1.C1.+(C1 x, C1 y)", 2); // Old implementation reports "operator +" (rather than "+")...

    [Fact]
    public Task TestConversionOperator()
        => TestAsync(
            """
            namespace N1
            {
                class C1
                {
                    public static explicit operator N1.C2(N1.C1 x)
                    {
                        $$return null;
                    }
                }
                class C2
                {
                }
            }
            """, "N1.C1.N1.C2(N1.C1 x)", 2); // Old implementation reports "explicit operator N1.C2" (rather than "N1.C2")...

    [Fact]
    public Task TestEvent()
        => TestAsync(
            """
            class C1
            {
                delegate void D1();
                event D1 e1$$;
            }
            """, "C1.e1", 0);

    [Fact]
    public Task TextExplicitInterfaceImplementation()
        => TestAsync(
            """
            interface I1
            {
                void M1();
            }
            class C1
            {
                void I1.M1()
                {
                $$}
            }
            """, "C1.M1()", 2);

    [Fact]
    public Task TextIndexer()
        => TestAsync(
            """
            class C1
            {
                C1 this[int x]
                {
                    get
                    {
                        $$return null;
                    }
                }
            }
            """, "C1.this[int x]", 4);

    [Fact]
    public Task TestParamsParameter()
        => TestAsync(
            """
            class C1
            {
                void M1(params int[] x) { $$ }
            }
            """, "C1.M1(params int[] x)", 0);

    [Fact]
    public Task TestArglistParameter()
        => TestAsync(
            """
            class C1
            {
                void M1(__arglist) { $$ }
            }
            """, "C1.M1(__arglist)", 0); // Old implementation does not show "__arglist"...

    [Fact]
    public Task TestRefAndOutParameters()
        => TestAsync(
            """
            class C1
            {
                void M1( ref int x, out int y )
                {
                    $$y = x;
                }
            }
            """, "C1.M1( ref int x, out int y )", 2); // Old implementation did not show extra spaces around the parameters...

    [Fact]
    public Task TestOptionalParameters()
        => TestAsync(
            """
            class C1
            {
                void M1(int x =1)
                {
                    $$y = x;
                }
            }
            """, "C1.M1(int x =1)", 2);

    [Fact]
    public Task TestExtensionMethod()
        => TestAsync(
            """
            static class C1
            {
                static void M1(this int x)
                {
                }$$
            }
            """, "C1.M1(this int x)", 2);

    [Fact]
    public Task TestGenericType()
        => TestAsync(
            """
            class C1<T, U>
            {
                static void M1() { $$ }
            }
            """, "C1.M1()", 0);

    [Fact]
    public Task TestGenericMethod()
        => TestAsync(
            """
            class C1<T, U>
            {
                static void M1<V>() { $$ }
            }
            """, "C1.M1()", 0);

    [Fact]
    public Task TestGenericParameters()
        => TestAsync(
            """
            class C1<T, U>
            {
                static void M1<V>(C1<int, V> x, V y) { $$ }
            }
            """, "C1.M1(C1<int, V> x, V y)", 0);

    [Fact]
    public Task TestMissingNamespace()
        => TestAsync(
            """
            {
                class Class
                {
                    int a1, a$$2;
                }
            }
            """, "Class.a2", 0);

    [Fact]
    public Task TestMissingNamespaceName()
        => TestAsync(
            """
            namespace
            {
                class C1
                {
                    int M1()
                    $${
                    }
                }
            }
            """, "?.C1.M1()", 1);

    [Fact]
    public Task TestMissingClassName()
        => TestAsync(
            """
            namespace N1
                class 
                {
                    int M1()
                    $${
                    }
                }
            }
            """, "N1.?.M1()", 1);

    [Fact]
    public Task TestMissingMethodName()
        => TestAsync(
            """
            namespace N1
            {
                class C1
                {
                    static void (ref int x)
                    {
                    $$}
                }
            }
            """, "N1.C1", 4);

    [Fact]
    public Task TestMissingParameterList()
        => TestAsync(
            """
            namespace N1
            {
                class C1
                {
                    static void M1
                    {
                    $$}
                }
            }
            """, "N1.C1.M1", 2);

    [Fact]
    public Task TopLevelField()
        => TestAsync(
            """
            $$int f1;
            """, "f1", 0, new CSharpParseOptions(kind: SourceCodeKind.Script));

    [Fact]
    public Task TopLevelMethod()
        => TestAsync(
            """
            int M1(int x)
            {
            $$}
            """, "M1(int x)", 2, new CSharpParseOptions(kind: SourceCodeKind.Script));

    [Fact]
    public Task TopLevelStatement()
        => TestAsync(
            """
            $$System.Console.WriteLine("Hello")
            """, null, 0, new CSharpParseOptions(kind: SourceCodeKind.Script));
}
