﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.VisualStudio.Razor.SyntaxVisualizer;

internal class RazorSyntaxNodeList(ChildSyntaxList childSyntaxList) : IEnumerable<RazorSyntaxNode>
{
    private readonly ChildSyntaxList _childSyntaxList = childSyntaxList;

    public IEnumerator<RazorSyntaxNode> GetEnumerator()
    {
        foreach (var nodeOrToken in _childSyntaxList)
        {
            yield return new RazorSyntaxNode(nodeOrToken);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
