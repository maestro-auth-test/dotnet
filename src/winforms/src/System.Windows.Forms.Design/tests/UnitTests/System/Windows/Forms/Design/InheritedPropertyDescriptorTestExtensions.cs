// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.ComponentModel;
using System.ComponentModel.Design;

namespace System.Windows.Forms.Design.Tests;

internal static class InheritedPropertyDescriptorTestExtensions
{
    public static InheritedPropertyDescriptor GetInheritedPropertyDescriptor(this Control control, string property)
    {
        PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(control);
        PropertyDescriptor propertyDescriptor = properties.Find(property, false);
        InheritedPropertyDescriptor inheritedPropertyDescriptor = new(propertyDescriptor, control);
        return inheritedPropertyDescriptor;
    }
}
