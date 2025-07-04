﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CodeAnalysis.CSharp;

using static SyntaxFactory;

internal static class CSharpSyntaxTokens
{
    public static readonly SyntaxToken AbstractKeyword = Token(SyntaxKind.AbstractKeyword);
    public static readonly SyntaxToken AssemblyKeyword = Token(SyntaxKind.AssemblyKeyword);
    public static readonly SyntaxToken AsyncKeyword = Token(SyntaxKind.AsyncKeyword);
    public static readonly SyntaxToken AwaitKeyword = Token(SyntaxKind.AwaitKeyword);
    public static readonly SyntaxToken BoolKeyword = Token(SyntaxKind.BoolKeyword);
    public static readonly SyntaxToken BreakKeyword = Token(SyntaxKind.BreakKeyword);
    public static readonly SyntaxToken ByteKeyword = Token(SyntaxKind.ByteKeyword);
    public static readonly SyntaxToken CaseKeyword = Token(SyntaxKind.CaseKeyword);
    public static readonly SyntaxToken CharKeyword = Token(SyntaxKind.CharKeyword);
    public static readonly SyntaxToken CheckedKeyword = Token(SyntaxKind.CheckedKeyword);
    public static readonly SyntaxToken CloseBraceToken = Token(SyntaxKind.CloseBraceToken);
    public static readonly SyntaxToken CloseBracketToken = Token(SyntaxKind.CloseBracketToken);
    public static readonly SyntaxToken CloseParenToken = Token(SyntaxKind.CloseParenToken);
    public static readonly SyntaxToken ColonToken = Token(SyntaxKind.ColonToken);
    public static readonly SyntaxToken CommaToken = Token(SyntaxKind.CommaToken);
    public static readonly SyntaxToken ConstKeyword = Token(SyntaxKind.ConstKeyword);
    public static readonly SyntaxToken ContinueKeyword = Token(SyntaxKind.ContinueKeyword);
    public static readonly SyntaxToken DecimalKeyword = Token(SyntaxKind.DecimalKeyword);
    public static readonly SyntaxToken DisableKeyword = Token(SyntaxKind.DisableKeyword);
    public static readonly SyntaxToken DotDotToken = Token(SyntaxKind.DotDotToken);
    public static readonly SyntaxToken DoubleKeyword = Token(SyntaxKind.DoubleKeyword);
    public static readonly SyntaxToken EndOfDocumentationCommentToken = Token(SyntaxKind.EndOfDocumentationCommentToken);
    public static readonly SyntaxToken EqualsToken = Token(SyntaxKind.EqualsToken);
    public static readonly SyntaxToken ExplicitKeyword = Token(SyntaxKind.ExplicitKeyword);
#if !ROSLYN_4_12_OR_LOWER
    public static readonly SyntaxToken ExtensionKeyword = Token(SyntaxKind.ExtensionKeyword);
#endif
    public static readonly SyntaxToken ExternKeyword = Token(SyntaxKind.ExternKeyword);
    public static readonly SyntaxToken FileKeyword = Token(SyntaxKind.FileKeyword);
    public static readonly SyntaxToken FixedKeyword = Token(SyntaxKind.FixedKeyword);
    public static readonly SyntaxToken FloatKeyword = Token(SyntaxKind.FloatKeyword);
    public static readonly SyntaxToken ForEachKeyword = Token(SyntaxKind.ForEachKeyword);
    public static readonly SyntaxToken FromKeyword = Token(SyntaxKind.FromKeyword);
    public static readonly SyntaxToken GlobalKeyword = Token(SyntaxKind.GlobalKeyword);
    public static readonly SyntaxToken GreaterThanEqualsToken = Token(SyntaxKind.GreaterThanEqualsToken);
    public static readonly SyntaxToken GreaterThanToken = Token(SyntaxKind.GreaterThanToken);
    public static readonly SyntaxToken IfKeyword = Token(SyntaxKind.IfKeyword);
    public static readonly SyntaxToken ImplicitKeyword = Token(SyntaxKind.ImplicitKeyword);
    public static readonly SyntaxToken InKeyword = Token(SyntaxKind.InKeyword);
    public static readonly SyntaxToken InterfaceKeyword = Token(SyntaxKind.InterfaceKeyword);
    public static readonly SyntaxToken InternalKeyword = Token(SyntaxKind.InternalKeyword);
    public static readonly SyntaxToken InterpolatedStringEndToken = Token(SyntaxKind.InterpolatedStringEndToken);
    public static readonly SyntaxToken InterpolatedStringStartToken = Token(SyntaxKind.InterpolatedStringStartToken);
    public static readonly SyntaxToken IntKeyword = Token(SyntaxKind.IntKeyword);
    public static readonly SyntaxToken IsKeyword = Token(SyntaxKind.IsKeyword);
    public static readonly SyntaxToken LessThanEqualsToken = Token(SyntaxKind.LessThanEqualsToken);
    public static readonly SyntaxToken LessThanToken = Token(SyntaxKind.LessThanToken);
    public static readonly SyntaxToken LetKeyword = Token(SyntaxKind.LetKeyword);
    public static readonly SyntaxToken LongKeyword = Token(SyntaxKind.LongKeyword);
    public static readonly SyntaxToken MethodKeyword = Token(SyntaxKind.MethodKeyword);
    public static readonly SyntaxToken NewKeyword = Token(SyntaxKind.NewKeyword);
    public static readonly SyntaxToken NotKeyword = Token(SyntaxKind.NotKeyword);
    public static readonly SyntaxToken NullKeyword = Token(SyntaxKind.NullKeyword);
    public static readonly SyntaxToken ObjectKeyword = Token(SyntaxKind.ObjectKeyword);
    public static readonly SyntaxToken OpenBraceToken = Token(SyntaxKind.OpenBraceToken);
    public static readonly SyntaxToken OpenBracketToken = Token(SyntaxKind.OpenBracketToken);
    public static readonly SyntaxToken OpenParenToken = Token(SyntaxKind.OpenParenToken);
    public static readonly SyntaxToken OperatorKeyword = Token(SyntaxKind.OperatorKeyword);
    public static readonly SyntaxToken OutKeyword = Token(SyntaxKind.OutKeyword);
    public static readonly SyntaxToken OverrideKeyword = Token(SyntaxKind.OverrideKeyword);
    public static readonly SyntaxToken ParamsKeyword = Token(SyntaxKind.ParamsKeyword);
    public static readonly SyntaxToken PartialKeyword = Token(SyntaxKind.PartialKeyword);
    public static readonly SyntaxToken PlusToken = Token(SyntaxKind.PlusToken);
    public static readonly SyntaxToken PrivateKeyword = Token(SyntaxKind.PrivateKeyword);
    public static readonly SyntaxToken PropertyKeyword = Token(SyntaxKind.PropertyKeyword);
    public static readonly SyntaxToken ProtectedKeyword = Token(SyntaxKind.ProtectedKeyword);
    public static readonly SyntaxToken PublicKeyword = Token(SyntaxKind.PublicKeyword);
    public static readonly SyntaxToken QuestionQuestionEqualsToken = Token(SyntaxKind.QuestionQuestionEqualsToken);
    public static readonly SyntaxToken QuestionToken = Token(SyntaxKind.QuestionToken);
    public static readonly SyntaxToken ReadOnlyKeyword = Token(SyntaxKind.ReadOnlyKeyword);
    public static readonly SyntaxToken RecordKeyword = Token(SyntaxKind.RecordKeyword);
    public static readonly SyntaxToken RefKeyword = Token(SyntaxKind.RefKeyword);
    public static readonly SyntaxToken RequiredKeyword = Token(SyntaxKind.RequiredKeyword);
    public static readonly SyntaxToken RestoreKeyword = Token(SyntaxKind.RestoreKeyword);
    public static readonly SyntaxToken ReturnKeyword = Token(SyntaxKind.ReturnKeyword);
    public static readonly SyntaxToken SByteKeyword = Token(SyntaxKind.SByteKeyword);
    public static readonly SyntaxToken ScopedKeyword = Token(SyntaxKind.ScopedKeyword);
    public static readonly SyntaxToken SealedKeyword = Token(SyntaxKind.SealedKeyword);
    public static readonly SyntaxToken SemicolonToken = Token(SyntaxKind.SemicolonToken);
    public static readonly SyntaxToken ShortKeyword = Token(SyntaxKind.ShortKeyword);
    public static readonly SyntaxToken SlashGreaterThanToken = Token(SyntaxKind.SlashGreaterThanToken);
    public static readonly SyntaxToken StaticKeyword = Token(SyntaxKind.StaticKeyword);
    public static readonly SyntaxToken StringKeyword = Token(SyntaxKind.StringKeyword);
    public static readonly SyntaxToken StructKeyword = Token(SyntaxKind.StructKeyword);
    public static readonly SyntaxToken SwitchKeyword = Token(SyntaxKind.SwitchKeyword);
    public static readonly SyntaxToken ThisKeyword = Token(SyntaxKind.ThisKeyword);
    public static readonly SyntaxToken TildeToken = Token(SyntaxKind.TildeToken);
    public static readonly SyntaxToken UIntKeyword = Token(SyntaxKind.UIntKeyword);
    public static readonly SyntaxToken ULongKeyword = Token(SyntaxKind.ULongKeyword);
    public static readonly SyntaxToken UnmanagedKeyword = Token(SyntaxKind.UnmanagedKeyword);
    public static readonly SyntaxToken UnsafeKeyword = Token(SyntaxKind.UnsafeKeyword);
    public static readonly SyntaxToken UShortKeyword = Token(SyntaxKind.UShortKeyword);
    public static readonly SyntaxToken UsingKeyword = Token(SyntaxKind.UsingKeyword);
    public static readonly SyntaxToken VirtualKeyword = Token(SyntaxKind.VirtualKeyword);
    public static readonly SyntaxToken VoidKeyword = Token(SyntaxKind.VoidKeyword);
    public static readonly SyntaxToken VolatileKeyword = Token(SyntaxKind.VolatileKeyword);
    public static readonly SyntaxToken WhereKeyword = Token(SyntaxKind.WhereKeyword);
}
