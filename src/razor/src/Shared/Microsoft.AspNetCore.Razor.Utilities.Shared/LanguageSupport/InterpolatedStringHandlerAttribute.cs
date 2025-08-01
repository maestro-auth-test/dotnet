﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copied from https://github.com/dotnet/runtime

#if !NET6_0_OR_GREATER

namespace System.Runtime.CompilerServices;

/// <summary>Indicates the attributed type is to be used as an interpolated string handler.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal sealed class InterpolatedStringHandlerAttribute : Attribute
{
    /// <summary>Initializes the <see cref="InterpolatedStringHandlerAttribute"/>.</summary>
    public InterpolatedStringHandlerAttribute() { }
}

#else

using System.Runtime.CompilerServices;

#pragma warning disable RS0016 // Add public types and members to the declared API (this is a supporting forwarder for an internal polyfill API)
[assembly: TypeForwardedTo(typeof(InterpolatedStringHandlerAttribute))]
#pragma warning restore RS0016 // Add public types and members to the declared API

#endif
