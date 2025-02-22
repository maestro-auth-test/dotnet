﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading.Tasks
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.VisualStudio.GraphModel
Imports Roslyn.Test.Utilities

Namespace Microsoft.VisualStudio.LanguageServices.UnitTests.Progression
    <UseExportProvider, Trait(Traits.Feature, Traits.Features.Progression)>
    Public Class GraphNodeIdTests
        Private Shared Async Function AssertMarkedNodeIdIsAsync(code As String, expectedId As String, Optional language As String = "C#", Optional symbolTransform As Func(Of ISymbol, ISymbol) = Nothing) As Task
            Using testState = ProgressionTestState.Create(
                <Workspace>
                    <Project Language=<%= language %> CommonReferences="true" FilePath="Z:\Project.csproj">
                        <Document FilePath="Z:\Project.cs">
                            <%= code %>
                        </Document>
                    </Project>
                </Workspace>)

                Dim graph = await testState.GetGraphWithMarkedSymbolNodeAsync(symbolTransform)
                Dim node = graph.Nodes.Single()
                Assert.Equal(expectedId, node.Id.ToString())
            End Using
        End Function

        <WpfFact>
        Public Async Function TestSimpleType() As Task
            Await AssertMarkedNodeIdIsAsync("namespace N { class $$C { } }", "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=C)")
        End Function

        <WpfFact>
        Public Async Function TestNestedType() As Task
            Await AssertMarkedNodeIdIsAsync("namespace N { class C { class $$E { } } }", "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=(Name=E ParentType=C))")
        End Function

        <WpfFact>
        Public Async Function TestMemberWithSimpleArrayType() As Task
            Await AssertMarkedNodeIdIsAsync(
                "namespace N { class C { void $$M(int[] p) { } } }",
                "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=C Member=(Name=M OverloadingParameters=[(Assembly=file:///Z:/FxReferenceAssembliesUri Namespace=System Type=(Name=Int32 ArrayRank=1 ParentType=Int32))]))")
        End Function

        <WpfFact>
        Public Async Function TestMemberWithNestedArrayType() As Task
            Await AssertMarkedNodeIdIsAsync(
                "namespace N { class C { void $$M(int[][,] p) { } } }",
                "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=C Member=(Name=M OverloadingParameters=[(Assembly=file:///Z:/FxReferenceAssembliesUri Namespace=System Type=(Name=Int32 ArrayRank=1 ParentType=(Name=Int32 ArrayRank=2 ParentType=Int32)))]))")
        End Function

        <WpfFact>
        Public Async Function TestMemberWithPointerType() As Task
            Await AssertMarkedNodeIdIsAsync(
                "namespace N { class C { struct S { } unsafe void $$M(S** p) { } } }",
                "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=C Member=(Name=M OverloadingParameters=[(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=(Name=S Indirection=2 ParentType=C))]))")
        End Function

        <WpfFact>
        Public Async Function TestMemberWithVoidPointerType() As Task
            Await AssertMarkedNodeIdIsAsync(
                "namespace N { class C { unsafe void $$M(void* p) { } } }",
                "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=C Member=(Name=M OverloadingParameters=[(Assembly=file:///Z:/FxReferenceAssembliesUri Namespace=System Type=(Name=Void Indirection=1))]))")
        End Function

        <WpfFact>
        Public Async Function TestMemberWithGenericTypeParameters() As Task
            Await AssertMarkedNodeIdIsAsync(
                "namespace N { class C<T> { void $$M<U>(T t, U u) { } } }",
                "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=(Name=C GenericParameterCount=1) Member=(Name=M GenericParameterCount=1 OverloadingParameters=[(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=(Name=C GenericParameterCount=1) ParameterIdentifier=0),(ParameterIdentifier=0)]))")
        End Function

        <WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/547263")>
        Public Async Function TestMemberWithParameterTypeConstructedWithMemberTypeParameter() As Task
            Await AssertMarkedNodeIdIsAsync(
                "namespace N { class C { void $$M<T>(T t, System.Func<T, int> u) { } } }",
                "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=C Member=(Name=M GenericParameterCount=1 OverloadingParameters=[(ParameterIdentifier=0),(Assembly=file:///Z:/FxReferenceAssembliesUri Namespace=System Type=(Name=Func GenericParameterCount=2 GenericArguments=[(ParameterIdentifier=0),(Assembly=file:///Z:/FxReferenceAssembliesUri Namespace=System Type=Int32)]))]))")
        End Function

        <WpfFact>
        Public Async Function TestMemberWithArraysOfGenericTypeParameters() As Task
            Await AssertMarkedNodeIdIsAsync(
                "namespace N { class C<T> { void $$M<U>(T[] t, U[] u) { } } }",
                "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=(Name=C GenericParameterCount=1) Member=(Name=M GenericParameterCount=1 OverloadingParameters=[(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=(ArrayRank=1 ParentType=(Type=(Name=C GenericParameterCount=1) ParameterIdentifier=0))),(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=(ArrayRank=1 ParentType=(ParameterIdentifier=0)))]))")
        End Function

        <WpfFact>
        Public Async Function TestMemberWithArraysOfGenericTypeParameters2() As Task
            Await AssertMarkedNodeIdIsAsync(
                "namespace N { class C<T> { void $$M<U>(T[][,] t, U[][,] u) { } } }",
                "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=(Name=C GenericParameterCount=1) Member=(Name=M GenericParameterCount=1 OverloadingParameters=[(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=(ArrayRank=1 ParentType=(ArrayRank=2 ParentType=(Type=(Name=C GenericParameterCount=1) ParameterIdentifier=0)))),(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=(ArrayRank=1 ParentType=(ArrayRank=2 ParentType=(ParameterIdentifier=0))))]))")
        End Function

        <WpfFact>
        Public Async Function TestMemberWithGenericType() As Task
            Await AssertMarkedNodeIdIsAsync(
                "namespace N { class C { void $$M(System.Collections.Generic.List<int> p) { } } }",
                "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=C Member=(Name=M OverloadingParameters=[(Assembly=file:///Z:/FxReferenceAssembliesUri Namespace=System.Collections.Generic Type=(Name=List GenericParameterCount=1 GenericArguments=[(Assembly=file:///Z:/FxReferenceAssembliesUri Namespace=System Type=Int32)]))]))")
        End Function

        <WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/616549")>
        Public Async Function TestMemberWithDynamicType() As Task
            Await AssertMarkedNodeIdIsAsync(
                "namespace N { class C { void $$M(dynamic d) { } } }",
                "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=C Member=(Name=M OverloadingParameters=[(Namespace=System Type=Object)]))")
        End Function

        <WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/616549")>
        Public Async Function TestMemberWithGenericTypeOfDynamicType() As Task
            Await AssertMarkedNodeIdIsAsync(
                "namespace N { class C { void $$M(System.Collections.Generic.List<dynamic> p) { } } }",
                "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=C Member=(Name=M OverloadingParameters=[(Assembly=file:///Z:/FxReferenceAssembliesUri Namespace=System.Collections.Generic Type=(Name=List GenericParameterCount=1 GenericArguments=[(Namespace=System Type=Object)]))]))")
        End Function

        <WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/616549")>
        Public Async Function TestMemberWithArrayOfDynamicType() As Task
            Await AssertMarkedNodeIdIsAsync(
                "namespace N { class C { void $$M(dynamic[] d) { } } }",
                "(Assembly=file:///Z:/bin/CSharpAssembly1.dll Namespace=N Type=C Member=(Name=M OverloadingParameters=[(Namespace=System Type=(Name=Object ArrayRank=1 ParentType=Object))]))")
        End Function

        <WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/547234")>
        Public Async Function TestErrorType() As Task
            Await AssertMarkedNodeIdIsAsync(
                "Class $$C : Inherits D : End Class",
                "Type=D",
                LanguageNames.VisualBasic,
                Function(s) DirectCast(s, INamedTypeSymbol).BaseType)
        End Function
    End Class
End Namespace
