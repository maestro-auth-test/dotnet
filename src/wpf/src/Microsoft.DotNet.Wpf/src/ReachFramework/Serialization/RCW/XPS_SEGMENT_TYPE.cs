// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Xps.Serialization.RCW
{
    /// <summary>
    /// RCW for xpsobjectmodel.idl found in Windows SDK
    /// This is generated code with minor manual edits. 
    /// i.  Generate TLB
    ///      MIDL /TLB xpsobjectmodel.tlb xpsobjectmodel.IDL //xpsobjectmodel.IDL found in Windows SDK
    /// ii. Generate RCW in a DLL
    ///      TLBIMP xpsobjectmodel.tlb // Generates xpsobjectmodel.dll
    /// iii.Decompile the DLL and copy out the RCW by hand.
    ///      ILDASM xpsobjectmodel.dll
    /// </summary>

    internal enum XPS_SEGMENT_TYPE
    {
        XPS_SEGMENT_TYPE_ARC_LARGE_CLOCKWISE = 1,
        XPS_SEGMENT_TYPE_ARC_LARGE_COUNTERCLOCKWISE = 2,
        XPS_SEGMENT_TYPE_ARC_SMALL_CLOCKWISE = 3,
        XPS_SEGMENT_TYPE_ARC_SMALL_COUNTERCLOCKWISE = 4,
        XPS_SEGMENT_TYPE_BEZIER = 5,
        XPS_SEGMENT_TYPE_LINE = 6,
        XPS_SEGMENT_TYPE_QUADRATIC_BEZIER = 7
    }
}