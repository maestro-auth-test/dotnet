// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore;

#nullable disable

public class PropertyValuesSqliteTest(PropertyValuesSqliteTest.PropertyValuesSqliteFixture fixture)
    : PropertyValuesRelationalTestBase<PropertyValuesSqliteTest.PropertyValuesSqliteFixture>(fixture)
{
    public class PropertyValuesSqliteFixture : PropertyValuesRelationalFixture
    {
        protected override ITestStoreFactory TestStoreFactory
            => SqliteTestStoreFactory.Instance;
    }
}
