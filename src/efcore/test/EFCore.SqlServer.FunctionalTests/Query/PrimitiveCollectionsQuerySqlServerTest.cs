// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.Query;

public class PrimitiveCollectionsQuerySqlServerTest : PrimitiveCollectionsQueryRelationalTestBase<
    PrimitiveCollectionsQuerySqlServerTest.PrimitiveCollectionsQuerySqlServerFixture>
{
    public override int? NumberOfValuesForHugeParameterCollectionTests { get; } = 5000;

    public PrimitiveCollectionsQuerySqlServerTest(PrimitiveCollectionsQuerySqlServerFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    public override async Task Inline_collection_of_ints_Contains()
    {
        await base.Inline_collection_of_ints_Contains();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Int] IN (10, 999)
""");
    }

    public override async Task Inline_collection_of_nullable_ints_Contains()
    {
        await base.Inline_collection_of_nullable_ints_Contains();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableInt] IN (10, 999)
""");
    }

    public override async Task Inline_collection_of_nullable_ints_Contains_null()
    {
        await base.Inline_collection_of_nullable_ints_Contains_null();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableInt] IS NULL OR [p].[NullableInt] = 999
""");
    }

    public override async Task Inline_collection_Count_with_zero_values()
    {
        await base.Inline_collection_Count_with_zero_values();

        AssertSql();
    }

    public override async Task Inline_collection_Count_with_one_value()
    {
        await base.Inline_collection_Count_with_one_value();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (VALUES (CAST(2 AS int))) AS [v]([Value])
    WHERE [v].[Value] > [p].[Id]) = 1
""");
    }

    public override async Task Inline_collection_Count_with_two_values()
    {
        await base.Inline_collection_Count_with_two_values();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (VALUES (CAST(2 AS int)), (999)) AS [v]([Value])
    WHERE [v].[Value] > [p].[Id]) = 1
""");
    }

    public override async Task Inline_collection_Count_with_three_values()
    {
        await base.Inline_collection_Count_with_three_values();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (VALUES (CAST(2 AS int)), (999), (1000)) AS [v]([Value])
    WHERE [v].[Value] > [p].[Id]) = 2
""");
    }

    public override async Task Inline_collection_Contains_with_zero_values()
    {
        await base.Inline_collection_Contains_with_zero_values();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE 0 = 1
""");
    }

    public override async Task Inline_collection_Contains_with_one_value()
    {
        await base.Inline_collection_Contains_with_one_value();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Id] = 2
""");
    }

    public override async Task Inline_collection_Contains_with_two_values()
    {
        await base.Inline_collection_Contains_with_two_values();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Id] IN (2, 999)
""");
    }

    public override async Task Inline_collection_Contains_with_three_values()
    {
        await base.Inline_collection_Contains_with_three_values();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Id] IN (2, 999, 1000)
""");
    }

    public override async Task Inline_collection_Contains_with_all_parameters()
    {
        await base.Inline_collection_Contains_with_all_parameters();

        AssertSql(
            """
@i='2'
@j='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Id] IN (@i, @j)
""");
    }

    public override async Task Inline_collection_Contains_with_constant_and_parameter()
    {
        await base.Inline_collection_Contains_with_constant_and_parameter();

        AssertSql(
            """
@j='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Id] IN (2, @j)
""");
    }

    public override async Task Inline_collection_Contains_with_mixed_value_types()
    {
        await base.Inline_collection_Contains_with_mixed_value_types();

        AssertSql(
            """
@i='11'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Int] IN (999, @i, [p].[Id], [p].[Id] + [p].[Int])
""");
    }

    public override async Task Inline_collection_List_Contains_with_mixed_value_types()
    {
        await base.Inline_collection_List_Contains_with_mixed_value_types();

        AssertSql(
            """
@i='11'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Int] IN (999, @i, [p].[Id], [p].[Id] + [p].[Int])
""");
    }

    public override async Task Inline_collection_Contains_as_Any_with_predicate()
    {
        await base.Inline_collection_Contains_as_Any_with_predicate();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Id] IN (2, 999)
""");
    }

    public override async Task Inline_collection_negated_Contains_as_All()
    {
        await base.Inline_collection_negated_Contains_as_All();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Id] NOT IN (2, 999)
""");
    }

    public override async Task Inline_collection_Min_with_two_values()
    {
        await base.Inline_collection_Min_with_two_values();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT MIN([v].[Value])
    FROM (VALUES (CAST(30 AS int)), ([p].[Int])) AS [v]([Value])) = 30
""");
    }

    public override async Task Inline_collection_List_Min_with_two_values()
    {
        await base.Inline_collection_List_Min_with_two_values();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT MIN([v].[Value])
    FROM (VALUES (CAST(30 AS int)), ([p].[Int])) AS [v]([Value])) = 30
""");
    }

    public override async Task Inline_collection_Max_with_two_values()
    {
        await base.Inline_collection_Max_with_two_values();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT MAX([v].[Value])
    FROM (VALUES (CAST(30 AS int)), ([p].[Int])) AS [v]([Value])) = 30
""");
    }

    public override async Task Inline_collection_List_Max_with_two_values()
    {
        await base.Inline_collection_List_Max_with_two_values();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT MAX([v].[Value])
    FROM (VALUES (CAST(30 AS int)), ([p].[Int])) AS [v]([Value])) = 30
""");
    }

    public override async Task Inline_collection_Min_with_three_values()
    {
        await base.Inline_collection_Min_with_three_values();

        AssertSql(
            """
@i='25'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT MIN([v].[Value])
    FROM (VALUES (CAST(30 AS int)), ([p].[Int]), (@i)) AS [v]([Value])) = 25
""");
    }

    public override async Task Inline_collection_List_Min_with_three_values()
    {
        await base.Inline_collection_List_Min_with_three_values();

        AssertSql(
            """
@i='25'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT MIN([v].[Value])
    FROM (VALUES (CAST(30 AS int)), ([p].[Int]), (@i)) AS [v]([Value])) = 25
""");
    }

    public override async Task Inline_collection_Max_with_three_values()
    {
        await base.Inline_collection_Max_with_three_values();

        AssertSql(
            """
@i='35'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT MAX([v].[Value])
    FROM (VALUES (CAST(30 AS int)), ([p].[Int]), (@i)) AS [v]([Value])) = 35
""");
    }

    public override async Task Inline_collection_List_Max_with_three_values()
    {
        await base.Inline_collection_List_Max_with_three_values();

        AssertSql(
            """
@i='35'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT MAX([v].[Value])
    FROM (VALUES (CAST(30 AS int)), ([p].[Int]), (@i)) AS [v]([Value])) = 35
""");
    }

    public override async Task Inline_collection_of_nullable_value_type_Min()
    {
        await base.Inline_collection_of_nullable_value_type_Min();

        AssertSql(
            """
@i='25' (Nullable = true)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT MIN([v].[Value])
    FROM (VALUES (CAST(30 AS int)), ([p].[Int]), (@i)) AS [v]([Value])) = 25
""");
    }

    public override async Task Inline_collection_of_nullable_value_type_Max()
    {
        await base.Inline_collection_of_nullable_value_type_Max();

        AssertSql(
            """
@i='35' (Nullable = true)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT MAX([v].[Value])
    FROM (VALUES (CAST(30 AS int)), ([p].[Int]), (@i)) AS [v]([Value])) = 35
""");
    }

    public override async Task Inline_collection_of_nullable_value_type_with_null_Min()
    {
        await base.Inline_collection_of_nullable_value_type_with_null_Min();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT MIN([v].[Value])
    FROM (VALUES (CAST(30 AS int)), ([p].[NullableInt]), (NULL)) AS [v]([Value])) = 30
""");
    }

    public override async Task Inline_collection_of_nullable_value_type_with_null_Max()
    {
        await base.Inline_collection_of_nullable_value_type_with_null_Max();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT MAX([v].[Value])
    FROM (VALUES (CAST(30 AS int)), ([p].[NullableInt]), (NULL)) AS [v]([Value])) = 30
""");
    }

    public override async Task Inline_collection_with_single_parameter_element_Contains()
    {
        await base.Inline_collection_with_single_parameter_element_Contains();

        AssertSql(
            """
@i='2'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Id] = @i
""");
    }

    public override async Task Inline_collection_with_single_parameter_element_Count()
    {
        await base.Inline_collection_with_single_parameter_element_Count();

        AssertSql(
            """
@i='2'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (VALUES (CAST(@i AS int))) AS [v]([Value])
    WHERE [v].[Value] > [p].[Id]) = 1
""");
    }

    public override async Task Inline_collection_Contains_with_EF_Parameter()
    {
        await base.Inline_collection_Contains_with_EF_Parameter();

        AssertSql(
            """
@p='[2,999,1000]' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Id] IN (
    SELECT [p0].[value]
    FROM OPENJSON(@p) WITH ([value] int '$') AS [p0]
)
""");
    }

    public override async Task Inline_collection_Count_with_column_predicate_with_EF_Parameter()
    {
        await base.Inline_collection_Count_with_column_predicate_with_EF_Parameter();

        AssertSql(
            """
@p='[2,999,1000]' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM OPENJSON(@p) WITH ([value] int '$') AS [p0]
    WHERE [p0].[value] > [p].[Id]) = 2
""");
    }

    public override async Task Parameter_collection_Count()
    {
        await base.Parameter_collection_Count();

        AssertSql(
            """
@ids1='2'
@ids2='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (VALUES (@ids1), (@ids2)) AS [i]([Value])
    WHERE [i].[Value] > [p].[Id]) = 1
""");
    }

    public override async Task Parameter_collection_of_ints_Contains_int()
    {
        await base.Parameter_collection_of_ints_Contains_int();

        AssertSql(
            """
@ints1='10'
@ints2='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Int] IN (@ints1, @ints2)
""",
            //
            """
@ints1='10'
@ints2='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Int] NOT IN (@ints1, @ints2)
""");
    }

    public override async Task Parameter_collection_HashSet_of_ints_Contains_int()
    {
        await base.Parameter_collection_HashSet_of_ints_Contains_int();

        AssertSql(
            """
@ints1='10'
@ints2='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Int] IN (@ints1, @ints2)
""",
            //
            """
@ints1='10'
@ints2='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Int] NOT IN (@ints1, @ints2)
""");
    }

    public override async Task Parameter_collection_ImmutableArray_of_ints_Contains_int()
    {
        await base.Parameter_collection_ImmutableArray_of_ints_Contains_int();

        AssertSql(
            """
@ints1='10'
@ints2='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Int] IN (@ints1, @ints2)
""",
            //
            """
@ints1='10'
@ints2='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Int] NOT IN (@ints1, @ints2)
""");
    }

    public override async Task Parameter_collection_of_ints_Contains_nullable_int()
    {
        await base.Parameter_collection_of_ints_Contains_nullable_int();

        AssertSql(
            """
@ints1='10'
@ints2='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableInt] IN (@ints1, @ints2)
""",
            //
            """
@ints1='10'
@ints2='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableInt] NOT IN (@ints1, @ints2) OR [p].[NullableInt] IS NULL
""");
    }

    public override async Task Parameter_collection_of_nullable_ints_Contains_int()
    {
        await base.Parameter_collection_of_nullable_ints_Contains_int();

        AssertSql(
            """
@nullableInts1='10'
@nullableInts2='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Int] IN (@nullableInts1, @nullableInts2)
""",
            //
            """
@nullableInts1='10'
@nullableInts2='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Int] NOT IN (@nullableInts1, @nullableInts2)
""");
    }

    public override async Task Parameter_collection_of_nullable_ints_Contains_nullable_int()
    {
        await base.Parameter_collection_of_nullable_ints_Contains_nullable_int();

        AssertSql(
            """
@nullableInts1='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableInt] IS NULL OR [p].[NullableInt] = @nullableInts1
""",
            //
            """
@nullableInts1='999'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableInt] IS NOT NULL AND [p].[NullableInt] <> @nullableInts1
""");
    }

    public override async Task Parameter_collection_of_strings_Contains_string()
    {
        await base.Parameter_collection_of_strings_Contains_string();

        AssertSql(
            """
@strings1='10' (Size = 4000)
@strings2='999' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[String] IN (@strings1, @strings2)
""",
            //
            """
@strings1='10' (Size = 4000)
@strings2='999' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[String] NOT IN (@strings1, @strings2)
""");
    }

    public override async Task Parameter_collection_of_strings_Contains_nullable_string()
    {
        await base.Parameter_collection_of_strings_Contains_nullable_string();

        AssertSql(
            """
@strings1='10' (Size = 4000)
@strings2='999' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableString] IN (@strings1, @strings2)
""",
            //
            """
@strings1='10' (Size = 4000)
@strings2='999' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableString] NOT IN (@strings1, @strings2) OR [p].[NullableString] IS NULL
""");
    }

    public override async Task Parameter_collection_of_nullable_strings_Contains_string()
    {
        await base.Parameter_collection_of_nullable_strings_Contains_string();

        AssertSql(
            """
@strings1='10' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[String] = @strings1
""",
            //
            """
@strings1='10' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[String] <> @strings1
""");
    }

    public override async Task Parameter_collection_of_nullable_strings_Contains_nullable_string()
    {
        await base.Parameter_collection_of_nullable_strings_Contains_nullable_string();

        AssertSql(
            """
@strings1='999' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableString] IS NULL OR [p].[NullableString] = @strings1
""",
            //
            """
@strings1='999' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableString] IS NOT NULL AND [p].[NullableString] <> @strings1
""");
    }

    public override async Task Parameter_collection_of_DateTimes_Contains()
    {
        await base.Parameter_collection_of_DateTimes_Contains();

        AssertSql(
            """
@dateTimes1='2020-01-10T12:30:00.0000000Z' (DbType = DateTime)
@dateTimes2='9999-01-01T00:00:00.0000000Z' (DbType = DateTime)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[DateTime] IN (@dateTimes1, @dateTimes2)
""");
    }

    public override async Task Parameter_collection_of_bools_Contains()
    {
        await base.Parameter_collection_of_bools_Contains();

        AssertSql(
            """
@bools1='True'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Bool] = @bools1
""");
    }

    public override async Task Parameter_collection_of_enums_Contains()
    {
        await base.Parameter_collection_of_enums_Contains();

        AssertSql(
            """
@enums1='0'
@enums2='3'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Enum] IN (@enums1, @enums2)
""");
    }

    public override async Task Parameter_collection_null_Contains()
    {
        await base.Parameter_collection_null_Contains();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE 0 = 1
""");
    }

    public override async Task Parameter_collection_Contains_with_EF_Constant()
    {
        await base.Parameter_collection_Contains_with_EF_Constant();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Id] IN (2, 999, 1000)
""");
    }

    public override async Task Parameter_collection_Where_with_EF_Constant_Where_Any()
    {
        await base.Parameter_collection_Where_with_EF_Constant_Where_Any();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE EXISTS (
    SELECT 1
    FROM (VALUES (CAST(2 AS int)), (999), (1000)) AS [i]([Value])
    WHERE [i].[Value] > 0)
""");
    }

    public override async Task Parameter_collection_Count_with_column_predicate_with_EF_Constant()
    {
        await base.Parameter_collection_Count_with_column_predicate_with_EF_Constant();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (VALUES (CAST(2 AS int)), (999), (1000)) AS [i]([Value])
    WHERE [i].[Value] > [p].[Id]) = 2
""");
    }

    public override async Task Parameter_collection_Count_with_huge_number_of_values()
    {
        await base.Parameter_collection_Count_with_huge_number_of_values();

        Assert.Contains("OPENJSON(@ids) WITH ([value] int '$')", Fixture.TestSqlLoggerFactory.SqlStatements[0], StringComparison.Ordinal);
    }

    public override async Task Parameter_collection_of_ints_Contains_int_with_huge_number_of_values()
    {
        await base.Parameter_collection_of_ints_Contains_int_with_huge_number_of_values();

        Assert.Contains("OPENJSON(@ints) WITH ([value] int '$')", Fixture.TestSqlLoggerFactory.SqlStatements[0], StringComparison.Ordinal);
        Assert.Contains("OPENJSON(@ints) WITH ([value] int '$')", Fixture.TestSqlLoggerFactory.SqlStatements[1], StringComparison.Ordinal);
    }

    public override async Task Column_collection_of_ints_Contains()
    {
        await base.Column_collection_of_ints_Contains();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE 10 IN (
    SELECT [i].[value]
    FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
)
""");
    }

    public override async Task Column_collection_of_nullable_ints_Contains()
    {
        await base.Column_collection_of_nullable_ints_Contains();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE 10 IN (
    SELECT [n].[value]
    FROM OPENJSON([p].[NullableInts]) WITH ([value] int '$') AS [n]
)
""");
    }

    public override async Task Column_collection_of_nullable_ints_Contains_null()
    {
        await base.Column_collection_of_nullable_ints_Contains_null();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE EXISTS (
    SELECT 1
    FROM OPENJSON([p].[NullableInts]) WITH ([value] int '$') AS [n]
    WHERE [n].[value] IS NULL)
""");
    }

    public override async Task Column_collection_of_strings_contains_null()
    {
        await base.Column_collection_of_strings_contains_null();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE 0 = 1
""");
    }

    public override async Task Column_collection_of_nullable_strings_contains_null()
    {
        await base.Column_collection_of_nullable_strings_contains_null();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE EXISTS (
    SELECT 1
    FROM OPENJSON([p].[NullableStrings]) WITH ([value] nvarchar(max) '$') AS [n]
    WHERE [n].[value] IS NULL)
""");
    }

    public override async Task Column_collection_of_bools_Contains()
    {
        await base.Column_collection_of_bools_Contains();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE CAST(1 AS bit) IN (
    SELECT [b].[value]
    FROM OPENJSON([p].[Bools]) WITH ([value] bit '$') AS [b]
)
""");
    }

    [ConditionalFact]
    public virtual async Task Json_representation_of_bool_array()
    {
        await using var context = CreateContext();

        Assert.Equal(
            "[true,false]",
            await context.Database.SqlQuery<string>($"SELECT [Bools] AS [Value] FROM [PrimitiveCollectionsEntity] WHERE [Id] = 1")
                .SingleAsync());
    }

    public override async Task Column_collection_Count_method()
    {
        await base.Column_collection_Count_method();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM OPENJSON([p].[Ints]) AS [i]) = 2
""");
    }

    public override async Task Column_collection_Length()
    {
        await base.Column_collection_Length();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM OPENJSON([p].[Ints]) AS [i]) = 2
""");
    }

    public override async Task Column_collection_Count_with_predicate()
    {
        await base.Column_collection_Count_with_predicate();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
    WHERE [i].[value] > 1) = 2
""");
    }

    public override async Task Column_collection_Where_Count()
    {
        await base.Column_collection_Where_Count();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
    WHERE [i].[value] > 1) = 2
""");
    }

    public override async Task Column_collection_index_int()
    {
        await base.Column_collection_index_int();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE CAST(JSON_VALUE([p].[Ints], '$[1]') AS int) = 10
""");
    }

    public override async Task Column_collection_index_string()
    {
        await base.Column_collection_index_string();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE JSON_VALUE([p].[Strings], '$[1]') = N'10'
""");
    }

    public override async Task Column_collection_index_datetime()
    {
        await base.Column_collection_index_datetime();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE CAST(JSON_VALUE([p].[DateTimes], '$[1]') AS datetime2) = '2020-01-10T12:30:00.0000000Z'
""");
    }

    public override async Task Column_collection_index_beyond_end()
    {
        await base.Column_collection_index_beyond_end();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE CAST(JSON_VALUE([p].[Ints], '$[999]') AS int) = 10
""");
    }

    public override async Task Nullable_reference_column_collection_index_equals_nullable_column()
    {
        // TODO: This test is incorrect, see #33784
        await base.Nullable_reference_column_collection_index_equals_nullable_column();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE JSON_VALUE([p].[NullableStrings], '$[2]') = [p].[NullableString] OR (JSON_VALUE([p].[NullableStrings], '$[2]') IS NULL AND [p].[NullableString] IS NULL)
""");
    }

    public override async Task Non_nullable_reference_column_collection_index_equals_nullable_column()
    {
        await base.Non_nullable_reference_column_collection_index_equals_nullable_column();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE EXISTS (
    SELECT 1
    FROM OPENJSON([p].[Strings]) AS [s]) AND JSON_VALUE([p].[Strings], '$[1]') = [p].[NullableString]
""");
    }

    public override async Task Inline_collection_index_Column()
    {
        await base.Inline_collection_index_Column();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT [v].[Value]
    FROM (VALUES (0, CAST(1 AS int)), (1, 2), (2, 3)) AS [v]([_ord], [Value])
    ORDER BY [v].[_ord]
    OFFSET [p].[Int] ROWS FETCH NEXT 1 ROWS ONLY) = 1
""");
    }

    public override async Task Inline_collection_index_Column_with_EF_Constant()
    {
        await base.Inline_collection_index_Column_with_EF_Constant();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE CAST(JSON_VALUE(N'[1,2,3]', '$[' + CAST([p].[Int] AS nvarchar(max)) + ']') AS int) = 1
""");
    }

    public override async Task Inline_collection_value_index_Column()
    {
        await base.Inline_collection_value_index_Column();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT [v].[Value]
    FROM (VALUES (0, CAST(1 AS int)), (1, [p].[Int]), (2, 3)) AS [v]([_ord], [Value])
    ORDER BY [v].[_ord]
    OFFSET [p].[Int] ROWS FETCH NEXT 1 ROWS ONLY) = 1
""");
    }

    public override async Task Inline_collection_List_value_index_Column()
    {
        await base.Inline_collection_List_value_index_Column();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT [v].[Value]
    FROM (VALUES (0, CAST(1 AS int)), (1, [p].[Int]), (2, 3)) AS [v]([_ord], [Value])
    ORDER BY [v].[_ord]
    OFFSET [p].[Int] ROWS FETCH NEXT 1 ROWS ONLY) = 1
""");
    }

    [SqlServerCondition(SqlServerCondition.SupportsJsonPathExpressions)]
    public override async Task Parameter_collection_index_Column_equal_Column()
    {
        await base.Parameter_collection_index_Column_equal_Column();

        AssertSql(
            """
@ints='[0,2,3]' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE CAST(JSON_VALUE(@ints, '$[' + CAST([p].[Int] AS nvarchar(max)) + ']') AS int) = [p].[Int]
""");
    }

    [SqlServerCondition(SqlServerCondition.SupportsJsonPathExpressions)]
    public override async Task Parameter_collection_index_Column_equal_constant()
    {
        await base.Parameter_collection_index_Column_equal_constant();

        AssertSql(
            """
@ints='[1,2,3]' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE CAST(JSON_VALUE(@ints, '$[' + CAST([p].[Int] AS nvarchar(max)) + ']') AS int) = 1
""");
    }

    public override async Task Column_collection_ElementAt()
    {
        await base.Column_collection_ElementAt();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE CAST(JSON_VALUE([p].[Ints], '$[1]') AS int) = 10
""");
    }

    public override async Task Column_collection_First()
    {
        await base.Column_collection_First();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT TOP(1) CAST([i].[value] AS int) AS [value]
    FROM OPENJSON([p].[Ints]) AS [i]
    ORDER BY CAST([i].[key] AS int)) = 1
""");
    }

    public override async Task Column_collection_FirstOrDefault()
    {
        await base.Column_collection_FirstOrDefault();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE COALESCE((
    SELECT TOP(1) CAST([i].[value] AS int) AS [value]
    FROM OPENJSON([p].[Ints]) AS [i]
    ORDER BY CAST([i].[key] AS int)), 0) = 1
""");
    }

    public override async Task Column_collection_Single()
    {
        await base.Column_collection_Single();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT TOP(1) CAST([i].[value] AS int) AS [value]
    FROM OPENJSON([p].[Ints]) AS [i]
    ORDER BY CAST([i].[key] AS int)) = 1
""");
    }

    public override async Task Column_collection_SingleOrDefault()
    {
        await base.Column_collection_SingleOrDefault();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE COALESCE((
    SELECT TOP(1) CAST([i].[value] AS int) AS [value]
    FROM OPENJSON([p].[Ints]) AS [i]
    ORDER BY CAST([i].[key] AS int)), 0) = 1
""");
    }

    public override async Task Column_collection_Skip()
    {
        await base.Column_collection_Skip();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT 1 AS empty
        FROM OPENJSON([p].[Ints]) AS [i]
        ORDER BY CAST([i].[key] AS int)
        OFFSET 1 ROWS
    ) AS [i0]) = 2
""");
    }

    public override async Task Column_collection_Take()
    {
        await base.Column_collection_Take();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE 11 IN (
    SELECT TOP(2) CAST([i].[value] AS int) AS [value]
    FROM OPENJSON([p].[Ints]) AS [i]
    ORDER BY CAST([i].[key] AS int)
)
""");
    }

    public override async Task Column_collection_Skip_Take()
    {
        await base.Column_collection_Skip_Take();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE 11 IN (
    SELECT CAST([i].[value] AS int) AS [value]
    FROM OPENJSON([p].[Ints]) AS [i]
    ORDER BY CAST([i].[key] AS int)
    OFFSET 1 ROWS FETCH NEXT 2 ROWS ONLY
)
""");
    }

    public override async Task Column_collection_Where_Skip()
    {
        await base.Column_collection_Where_Skip();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT 1 AS empty
        FROM OPENJSON([p].[Ints]) AS [i]
        WHERE CAST([i].[value] AS int) > 1
        ORDER BY CAST([i].[key] AS int)
        OFFSET 1 ROWS
    ) AS [i0]) = 3
""");
    }

    public override async Task Column_collection_Where_Take()
    {
        await base.Column_collection_Where_Take();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT TOP(2) 1 AS empty
        FROM OPENJSON([p].[Ints]) AS [i]
        WHERE CAST([i].[value] AS int) > 1
        ORDER BY CAST([i].[key] AS int)
    ) AS [i0]) = 2
""");
    }

    public override async Task Column_collection_Where_Skip_Take()
    {
        await base.Column_collection_Where_Skip_Take();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT 1 AS empty
        FROM OPENJSON([p].[Ints]) AS [i]
        WHERE CAST([i].[value] AS int) > 1
        ORDER BY CAST([i].[key] AS int)
        OFFSET 1 ROWS FETCH NEXT 2 ROWS ONLY
    ) AS [i0]) = 1
""");
    }

    public override async Task Column_collection_Contains_over_subquery()
    {
        await base.Column_collection_Contains_over_subquery();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE 11 IN (
    SELECT [i].[value]
    FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
    WHERE [i].[value] > 1
)
""");
    }

    public override async Task Column_collection_OrderByDescending_ElementAt()
    {
        await base.Column_collection_OrderByDescending_ElementAt();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT [i].[value]
    FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
    ORDER BY [i].[value] DESC
    OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY) = 111
""");
    }

    public override async Task Column_collection_Where_ElementAt()
    {
        await base.Column_collection_Where_ElementAt();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT CAST([i].[value] AS int) AS [value]
    FROM OPENJSON([p].[Ints]) AS [i]
    WHERE CAST([i].[value] AS int) > 1
    ORDER BY CAST([i].[key] AS int)
    OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY) = 11
""");
    }

    public override async Task Column_collection_Any()
    {
        await base.Column_collection_Any();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE EXISTS (
    SELECT 1
    FROM OPENJSON([p].[Ints]) AS [i])
""");
    }

    public override async Task Column_collection_Distinct()
    {
        await base.Column_collection_Distinct();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT DISTINCT [i].[value]
        FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
    ) AS [i0]) = 3
""");
    }

    public override async Task Column_collection_SelectMany()
    {
        await base.Column_collection_SelectMany();

        AssertSql(
            """
SELECT [i].[value]
FROM [PrimitiveCollectionsEntity] AS [p]
CROSS APPLY OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
""");
    }

    public override async Task Column_collection_SelectMany_with_filter()
    {
        await base.Column_collection_SelectMany_with_filter();

        AssertSql(
            """
SELECT [i0].[value]
FROM [PrimitiveCollectionsEntity] AS [p]
CROSS APPLY (
    SELECT [i].[value]
    FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
    WHERE [i].[value] > 1
) AS [i0]
""");
    }

    public override async Task Column_collection_SelectMany_with_Select_to_anonymous_type()
    {
        await base.Column_collection_SelectMany_with_Select_to_anonymous_type();

        AssertSql(
            """
SELECT [i].[value] AS [Original], [i].[value] + 1 AS [Incremented]
FROM [PrimitiveCollectionsEntity] AS [p]
CROSS APPLY OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
""");
    }

    public override async Task Column_collection_projection_from_top_level()
    {
        await base.Column_collection_projection_from_top_level();

        AssertSql(
            """
SELECT [p].[Ints]
FROM [PrimitiveCollectionsEntity] AS [p]
ORDER BY [p].[Id]
""");
    }

    public override async Task Column_collection_Join_parameter_collection()
    {
        await base.Column_collection_Join_parameter_collection();

        AssertSql(
            """
@ints1='11'
@ints2='111'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
    INNER JOIN (VALUES (@ints1), (@ints2)) AS [i0]([Value]) ON [i].[value] = [i0].[Value]) = 2
""");
    }

    public override async Task Inline_collection_Join_ordered_column_collection()
    {
        await base.Inline_collection_Join_ordered_column_collection();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (VALUES (CAST(11 AS int)), (111)) AS [v]([Value])
    INNER JOIN OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i] ON [v].[Value] = [i].[value]) = 2
""");
    }

    public override async Task Parameter_collection_Concat_column_collection()
    {
        await base.Parameter_collection_Concat_column_collection();

        AssertSql(
            """
@ints1='11'
@ints2='111'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT 1 AS empty
        FROM (VALUES (@ints1), (@ints2)) AS [i]([Value])
        UNION ALL
        SELECT 1 AS empty
        FROM OPENJSON([p].[Ints]) AS [i0]
    ) AS [u]) = 2
""");
    }

    [SqlServerCondition(SqlServerCondition.SupportsJsonPathExpressions)]
    public override async Task Parameter_collection_with_type_inference_for_JsonScalarExpression()
    {
        await base.Parameter_collection_with_type_inference_for_JsonScalarExpression();

        AssertSql(
            """
@values='["one","two"]' (Size = 4000)

SELECT CASE
    WHEN [p].[Id] <> 0 THEN JSON_VALUE(@values, '$[' + CAST([p].[Int] % 2 AS nvarchar(max)) + ']')
    ELSE N'foo'
END
FROM [PrimitiveCollectionsEntity] AS [p]
""");
    }

    public override async Task Column_collection_Union_parameter_collection()
    {
        await base.Column_collection_Union_parameter_collection();

        AssertSql(
            """
@ints1='11'
@ints2='111'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT [i].[value]
        FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
        UNION
        SELECT [i0].[Value] AS [value]
        FROM (VALUES (@ints1), (@ints2)) AS [i0]([Value])
    ) AS [u]) = 2
""");
    }

    public override async Task Column_collection_Intersect_inline_collection()
    {
        await base.Column_collection_Intersect_inline_collection();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT [i].[value]
        FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
        INTERSECT
        SELECT [v].[Value] AS [value]
        FROM (VALUES (CAST(11 AS int)), (111)) AS [v]([Value])
    ) AS [i0]) = 2
""");
    }

    public override async Task Inline_collection_Except_column_collection()
    {
        await base.Inline_collection_Except_column_collection();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT [v].[Value]
        FROM (VALUES (CAST(11 AS int)), (111)) AS [v]([Value])
        EXCEPT
        SELECT [i].[value] AS [Value]
        FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
    ) AS [e]
    WHERE [e].[Value] % 2 = 1) = 2
""");
    }

    public override async Task Column_collection_Where_Union()
    {
        await base.Column_collection_Where_Union();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT [i].[value]
        FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
        WHERE [i].[value] > 100
        UNION
        SELECT [v].[Value] AS [value]
        FROM (VALUES (CAST(50 AS int))) AS [v]([Value])
    ) AS [u]) = 2
""");
    }

    public override async Task Column_collection_equality_parameter_collection()
    {
        await base.Column_collection_equality_parameter_collection();

        AssertSql(
            """
@ints='[1,10]' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Ints] = @ints
""");
    }

    public override async Task Column_collection_Concat_parameter_collection_equality_inline_collection()
    {
        await base.Column_collection_Concat_parameter_collection_equality_inline_collection();

        AssertSql();
    }

    public override async Task Column_collection_equality_inline_collection()
    {
        await base.Column_collection_equality_inline_collection();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Ints] = N'[1,10]'
""");
    }

    public override async Task Column_collection_equality_inline_collection_with_parameters()
    {
        await base.Column_collection_equality_inline_collection_with_parameters();

        AssertSql();
    }

    public override async Task Column_collection_Where_equality_inline_collection()
    {
        await base.Column_collection_Where_equality_inline_collection();

        AssertSql();
    }

    public override async Task Parameter_collection_in_subquery_Union_column_collection_as_compiled_query()
    {
        await base.Parameter_collection_in_subquery_Union_column_collection_as_compiled_query();

        AssertSql(
            """
@ints1='10'
@ints2='111'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT [i1].[Value]
        FROM (
            SELECT [i].[Value]
            FROM (VALUES (0, @ints1), (1, @ints2)) AS [i]([_ord], [Value])
            ORDER BY [i].[_ord]
            OFFSET 1 ROWS
        ) AS [i1]
        UNION
        SELECT [i0].[value] AS [Value]
        FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i0]
    ) AS [u]) = 3
""");
    }

    public override async Task Parameter_collection_in_subquery_Union_column_collection()
    {
        await base.Parameter_collection_in_subquery_Union_column_collection();

        AssertSql(
            """
@Skip1='111'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT [s].[Value]
        FROM (VALUES (@Skip1)) AS [s]([Value])
        UNION
        SELECT [i].[value] AS [Value]
        FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
    ) AS [u]) = 3
""");
    }

    public override async Task Parameter_collection_in_subquery_Union_column_collection_nested()
    {
        await base.Parameter_collection_in_subquery_Union_column_collection_nested();

        AssertSql(
            """
@Skip1='111'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT [s].[Value]
        FROM (VALUES (@Skip1)) AS [s]([Value])
        UNION
        SELECT [i2].[value] AS [Value]
        FROM (
            SELECT TOP(20) [i1].[value]
            FROM (
                SELECT DISTINCT [i0].[value]
                FROM (
                    SELECT [i].[value]
                    FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
                    ORDER BY [i].[value]
                    OFFSET 1 ROWS
                ) AS [i0]
            ) AS [i1]
            ORDER BY [i1].[value] DESC
        ) AS [i2]
    ) AS [u]) = 3
""");
    }

    public override void Parameter_collection_in_subquery_and_Convert_as_compiled_query()
    {
        base.Parameter_collection_in_subquery_and_Convert_as_compiled_query();

        AssertSql();
    }

    public override async Task Parameter_collection_in_subquery_Count_as_compiled_query()
    {
        await base.Parameter_collection_in_subquery_Count_as_compiled_query();

        // TODO: the subquery projection contains extra columns which we should remove
        AssertSql(
            """
@ints1='10'
@ints2='111'

SELECT COUNT(*)
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT [i].[Value] AS [Value0]
        FROM (VALUES (0, @ints1), (1, @ints2)) AS [i]([_ord], [Value])
        ORDER BY [i].[_ord]
        OFFSET 1 ROWS
    ) AS [i0]
    WHERE [i0].[Value0] > [p].[Id]) = 1
""");
    }

    public override async Task Parameter_collection_in_subquery_Union_another_parameter_collection_as_compiled_query()
    {
        await base.Parameter_collection_in_subquery_Union_another_parameter_collection_as_compiled_query();

        AssertSql();
    }

    public override async Task Column_collection_in_subquery_Union_parameter_collection()
    {
        await base.Column_collection_in_subquery_Union_parameter_collection();

        AssertSql(
            """
@ints1='10'
@ints2='111'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (
        SELECT [i1].[value]
        FROM (
            SELECT CAST([i].[value] AS int) AS [value]
            FROM OPENJSON([p].[Ints]) AS [i]
            ORDER BY CAST([i].[key] AS int)
            OFFSET 1 ROWS
        ) AS [i1]
        UNION
        SELECT [i0].[Value] AS [value]
        FROM (VALUES (@ints1), (@ints2)) AS [i0]([Value])
    ) AS [u]) = 3
""");
    }

    public override async Task Project_collection_of_ints_simple()
    {
        await base.Project_collection_of_ints_simple();

        AssertSql(
            """
SELECT [p].[Ints]
FROM [PrimitiveCollectionsEntity] AS [p]
ORDER BY [p].[Id]
""");
    }

    public override async Task Project_collection_of_ints_ordered()
    {
        await base.Project_collection_of_ints_ordered();

        AssertSql(
            """
SELECT [p].[Id], CAST([i].[value] AS int) AS [value], [i].[key]
FROM [PrimitiveCollectionsEntity] AS [p]
OUTER APPLY OPENJSON([p].[Ints]) AS [i]
ORDER BY [p].[Id], CAST([i].[value] AS int) DESC
""");
    }

    public override async Task Project_collection_of_datetimes_filtered()
    {
        await base.Project_collection_of_datetimes_filtered();

        AssertSql(
            """
SELECT [p].[Id], [d0].[value], [d0].[key]
FROM [PrimitiveCollectionsEntity] AS [p]
OUTER APPLY (
    SELECT CAST([d].[value] AS datetime2) AS [value], [d].[key], CAST([d].[key] AS int) AS [c]
    FROM OPENJSON([p].[DateTimes]) AS [d]
    WHERE DATEPART(day, CAST([d].[value] AS datetime2)) <> 1
) AS [d0]
ORDER BY [p].[Id], [d0].[c]
""");
    }

    public override async Task Project_collection_of_nullable_ints_with_paging()
    {
        await base.Project_collection_of_nullable_ints_with_paging();

        AssertSql(
            """
SELECT [p].[Id], [n0].[value], [n0].[key]
FROM [PrimitiveCollectionsEntity] AS [p]
OUTER APPLY (
    SELECT TOP(20) CAST([n].[value] AS int) AS [value], [n].[key], CAST([n].[key] AS int) AS [c]
    FROM OPENJSON([p].[NullableInts]) AS [n]
    ORDER BY CAST([n].[key] AS int)
) AS [n0]
ORDER BY [p].[Id], [n0].[c]
""");
    }

    public override async Task Project_collection_of_nullable_ints_with_paging2()
    {
        await base.Project_collection_of_nullable_ints_with_paging2();

        AssertSql(
            """
SELECT [p].[Id], [n0].[value], [n0].[key]
FROM [PrimitiveCollectionsEntity] AS [p]
OUTER APPLY (
    SELECT CAST([n].[value] AS int) AS [value], [n].[key]
    FROM OPENJSON([p].[NullableInts]) AS [n]
    ORDER BY CAST([n].[value] AS int)
    OFFSET 1 ROWS
) AS [n0]
ORDER BY [p].[Id], [n0].[value]
""");
    }

    public override async Task Project_collection_of_nullable_ints_with_paging3()
    {
        await base.Project_collection_of_nullable_ints_with_paging3();

        AssertSql(
            """
SELECT [p].[Id], [n0].[value], [n0].[key]
FROM [PrimitiveCollectionsEntity] AS [p]
OUTER APPLY (
    SELECT CAST([n].[value] AS int) AS [value], [n].[key], CAST([n].[key] AS int) AS [c]
    FROM OPENJSON([p].[NullableInts]) AS [n]
    ORDER BY CAST([n].[key] AS int)
    OFFSET 2 ROWS
) AS [n0]
ORDER BY [p].[Id], [n0].[c]
""");
    }

    public override async Task Project_collection_of_ints_with_distinct()
    {
        await base.Project_collection_of_ints_with_distinct();

        AssertSql(
            """
SELECT [p].[Id], [i0].[value]
FROM [PrimitiveCollectionsEntity] AS [p]
OUTER APPLY (
    SELECT DISTINCT [i].[value]
    FROM OPENJSON([p].[Ints]) WITH ([value] int '$') AS [i]
) AS [i0]
ORDER BY [p].[Id]
""");
    }

    public override async Task Project_collection_of_nullable_ints_with_distinct()
    {
        await base.Project_collection_of_nullable_ints_with_distinct();

        AssertSql("");
    }

    public override async Task Project_collection_of_ints_with_ToList_and_FirstOrDefault()
    {
        await base.Project_collection_of_ints_with_ToList_and_FirstOrDefault();

        AssertSql(
            """
SELECT [p0].[Id], CAST([i].[value] AS int) AS [value], [i].[key]
FROM (
    SELECT TOP(1) [p].[Id], [p].[Ints]
    FROM [PrimitiveCollectionsEntity] AS [p]
    ORDER BY [p].[Id]
) AS [p0]
OUTER APPLY OPENJSON([p0].[Ints]) AS [i]
ORDER BY [p0].[Id], CAST([i].[key] AS int)
""");
    }

    public override async Task Project_empty_collection_of_nullables_and_collection_only_containing_nulls()
    {
        await base.Project_empty_collection_of_nullables_and_collection_only_containing_nulls();

        AssertSql(
            """
SELECT [p].[Id], [n1].[value], [n1].[key], [n2].[value], [n2].[key]
FROM [PrimitiveCollectionsEntity] AS [p]
OUTER APPLY (
    SELECT CAST([n].[value] AS int) AS [value], [n].[key], CAST([n].[key] AS int) AS [c]
    FROM OPENJSON([p].[NullableInts]) AS [n]
    WHERE 0 = 1
) AS [n1]
OUTER APPLY (
    SELECT CAST([n0].[value] AS int) AS [value], [n0].[key], CAST([n0].[key] AS int) AS [c]
    FROM OPENJSON([p].[NullableInts]) AS [n0]
    WHERE [n0].[value] IS NULL
) AS [n2]
ORDER BY [p].[Id], [n1].[c], [n1].[key], [n2].[c]
""");
    }

    public override async Task Project_multiple_collections()
    {
        await base.Project_multiple_collections();

        AssertSql(
            """
SELECT [p].[Id], CAST([i].[value] AS int) AS [value], [i].[key], CAST([i0].[value] AS int) AS [value], [i0].[key], [d1].[value], [d1].[key], [d2].[value], [d2].[key]
FROM [PrimitiveCollectionsEntity] AS [p]
OUTER APPLY OPENJSON([p].[Ints]) AS [i]
OUTER APPLY OPENJSON([p].[Ints]) AS [i0]
OUTER APPLY (
    SELECT CAST([d].[value] AS datetime2) AS [value], [d].[key], CAST([d].[key] AS int) AS [c]
    FROM OPENJSON([p].[DateTimes]) AS [d]
    WHERE DATEPART(day, CAST([d].[value] AS datetime2)) <> 1
) AS [d1]
OUTER APPLY (
    SELECT CAST([d0].[value] AS datetime2) AS [value], [d0].[key], CAST([d0].[key] AS int) AS [c]
    FROM OPENJSON([p].[DateTimes]) AS [d0]
    WHERE CAST([d0].[value] AS datetime2) > '2000-01-01T00:00:00.0000000'
) AS [d2]
ORDER BY [p].[Id], CAST([i].[key] AS int), [i].[key], CAST([i0].[value] AS int) DESC, [i0].[key], [d1].[c], [d1].[key], [d2].[c]
""");
    }

    public override async Task Project_primitive_collections_element()
    {
        await base.Project_primitive_collections_element();

        AssertSql(
            """
SELECT CAST(JSON_VALUE([p].[Ints], '$[0]') AS int) AS [Indexer], CAST(JSON_VALUE([p].[DateTimes], '$[0]') AS datetime2) AS [EnumerableElementAt], JSON_VALUE([p].[Strings], '$[1]') AS [QueryableElementAt]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[Id] < 4
ORDER BY [p].[Id]
""");
    }

    public override async Task Project_inline_collection()
    {
        await base.Project_inline_collection();

        AssertSql(
            """
SELECT [p].[String]
FROM [PrimitiveCollectionsEntity] AS [p]
""");
    }

    public override async Task Project_inline_collection_with_Union()
    {
        await base.Project_inline_collection_with_Union();

        AssertSql(
            """
SELECT [p].[Id], [u].[Value]
FROM [PrimitiveCollectionsEntity] AS [p]
OUTER APPLY (
    SELECT [v].[Value]
    FROM (VALUES ([p].[String])) AS [v]([Value])
    UNION
    SELECT [p0].[String] AS [Value]
    FROM [PrimitiveCollectionsEntity] AS [p0]
) AS [u]
ORDER BY [p].[Id]
""");
    }

    public override async Task Project_inline_collection_with_Concat()
    {
        await base.Project_inline_collection_with_Concat();

        AssertSql();
    }

    public override async Task Nested_contains_with_Lists_and_no_inferred_type_mapping()
    {
        await base.Nested_contains_with_Lists_and_no_inferred_type_mapping();

        AssertSql(
            """
@ints1='1'
@ints2='2'
@ints3='3'
@strings1='one' (Size = 4000)
@strings2='two' (Size = 4000)
@strings3='three' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE CASE
    WHEN [p].[Int] IN (@ints1, @ints2, @ints3) THEN N'one'
    ELSE N'two'
END IN (@strings1, @strings2, @strings3)
""");
    }

    public override async Task Nested_contains_with_arrays_and_no_inferred_type_mapping()
    {
        await base.Nested_contains_with_arrays_and_no_inferred_type_mapping();

        AssertSql(
            """
@ints1='1'
@ints2='2'
@ints3='3'
@strings1='one' (Size = 4000)
@strings2='two' (Size = 4000)
@strings3='three' (Size = 4000)

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE CASE
    WHEN [p].[Int] IN (@ints1, @ints2, @ints3) THEN N'one'
    ELSE N'two'
END IN (@strings1, @strings2, @strings3)
""");
    }

    public override async Task Parameter_collection_of_structs_Contains_struct()
    {
        await base.Parameter_collection_of_structs_Contains_struct();

        AssertSql(
            """
@values1='22'
@values2='33'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[WrappedId] IN (@values1, @values2)
""",
            //
            """
@values1='11'
@values2='44'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[WrappedId] NOT IN (@values1, @values2)
""");
    }

    public override async Task Parameter_collection_of_structs_Contains_nullable_struct()
    {
        await base.Parameter_collection_of_structs_Contains_nullable_struct();

        AssertSql(
            """
@values1='22'
@values2='33'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableWrappedId] IN (@values1, @values2)
""",
            //
            """
@values1='11'
@values2='44'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableWrappedId] NOT IN (@values1, @values2) OR [p].[NullableWrappedId] IS NULL
""");
    }

    public override async Task Parameter_collection_of_structs_Contains_nullable_struct_with_nullable_comparer()
    {
        await base.Parameter_collection_of_structs_Contains_nullable_struct_with_nullable_comparer();

        AssertSql(
            """
@values1='22'
@values2='33'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableWrappedIdWithNullableComparer] IN (@values1, @values2)
""",
            //
            """
@values1='11'
@values2='44'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableWrappedId] NOT IN (@values1, @values2) OR [p].[NullableWrappedId] IS NULL
""");
    }

    public override async Task Parameter_collection_of_nullable_structs_Contains_struct()
    {
        await base.Parameter_collection_of_nullable_structs_Contains_struct();

        AssertSql(
            """
@values1='22'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[WrappedId] = @values1
""",
            //
            """
@values1='11'
@values2='44'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[WrappedId] NOT IN (@values1, @values2)
""");
    }

    public override async Task Parameter_collection_of_nullable_structs_Contains_nullable_struct()
    {
        await base.Parameter_collection_of_nullable_structs_Contains_nullable_struct();

        AssertSql(
            """
@values1='22'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableWrappedId] IS NULL OR [p].[NullableWrappedId] = @values1
""",
            //
            """
@values1='11'
@values2='44'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableWrappedId] NOT IN (@values1, @values2) OR [p].[NullableWrappedId] IS NULL
""");
    }

    public override async Task Parameter_collection_of_nullable_structs_Contains_nullable_struct_with_nullable_comparer()
    {
        await base.Parameter_collection_of_nullable_structs_Contains_nullable_struct_with_nullable_comparer();

        AssertSql(
            """
@values1='22'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableWrappedIdWithNullableComparer] IS NULL OR [p].[NullableWrappedIdWithNullableComparer] = @values1
""",
            //
            """
@values1='11'
@values2='44'

SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE [p].[NullableWrappedIdWithNullableComparer] NOT IN (@values1, @values2) OR [p].[NullableWrappedIdWithNullableComparer] IS NULL
""");
    }

    public override async Task Values_of_enum_casted_to_underlying_value()
    {
        await base.Values_of_enum_casted_to_underlying_value();

        AssertSql(
            """
SELECT [p].[Id], [p].[Bool], [p].[Bools], [p].[DateTime], [p].[DateTimes], [p].[Enum], [p].[Enums], [p].[Int], [p].[Ints], [p].[NullableInt], [p].[NullableInts], [p].[NullableString], [p].[NullableStrings], [p].[NullableWrappedId], [p].[NullableWrappedIdWithNullableComparer], [p].[String], [p].[Strings], [p].[WrappedId]
FROM [PrimitiveCollectionsEntity] AS [p]
WHERE (
    SELECT COUNT(*)
    FROM (VALUES (CAST(0 AS int)), (1), (2), (3)) AS [v]([Value])
    WHERE [v].[Value] = [p].[Int]) > 0
""");
    }

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => TestHelpers.AssertAllMethodsOverridden(GetType());

    private void AssertSql(params string[] expected)
        => Fixture.TestSqlLoggerFactory.AssertBaseline(expected);

    private PrimitiveCollectionsContext CreateContext()
        => Fixture.CreateContext();

    public class PrimitiveCollectionsQuerySqlServerFixture : PrimitiveCollectionsQueryFixtureBase, ITestSqlLoggerFactory
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory
            => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory
            => SqlServerTestStoreFactory.Instance;

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            // Map DateTime to non-default datetime instead of the default datetime2 to exercise type mapping inference
            modelBuilder.Entity<PrimitiveCollectionsEntity>().Property(p => p.DateTime).HasColumnType("datetime");
        }
    }
}
