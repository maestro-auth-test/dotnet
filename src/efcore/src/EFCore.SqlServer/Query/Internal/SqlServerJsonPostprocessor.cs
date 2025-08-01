// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.SqlServer.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal;
using ColumnInfo = Microsoft.EntityFrameworkCore.SqlServer.Query.Internal.SqlServerOpenJsonExpression.ColumnInfo;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;

/// <summary>
///     Performs various post-processing rewriting to account for SQL Server JSON quirks.
///     1. Converts <see cref="SqlServerOpenJsonExpression" /> expressions with WITH (the default) to OPENJSON without WITH under the
///     following conditions:
///     * When an ordering still exists on the [key] column, i.e. when the ordering of the original JSON array needs to be preserved
///     (e.g. limit/offset).
///     * When the column type in the WITH clause is a SQL Server "CLR type" - these are incompatible with WITH (e.g. hierarchy id).
///     2. Rewrite JsonScalarExpression (JSON_VALUE()) to OPENJSON for when JSON_VALUE() isn't compatible with the type (e.g. binary data
///     which needs to be base64-decoded).
/// </summary>
/// <remarks>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </remarks>
public sealed class SqlServerJsonPostprocessor(
    IRelationalTypeMappingSource typeMappingSource,
    ISqlExpressionFactory sqlExpressionFactory,
    SqlAliasManager? sqlAliasManager)
    : ExpressionVisitor
{
    private readonly List<OuterApplyExpression> _openjsonOuterAppliesToAdd = new();
    private readonly Dictionary<(string, string), ColumnInfo> _columnsToRewrite = new();

    private RelationalTypeMapping? _nvarcharMaxTypeMapping;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public Expression Process(Expression expression)
    {
        _columnsToRewrite.Clear();

        return Visit(expression);
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    [return: NotNullIfNotNull(nameof(expression))]
    public override Expression? Visit(Expression? expression)
    {
        switch (expression)
        {
            case ShapedQueryExpression shapedQueryExpression:
                shapedQueryExpression = shapedQueryExpression
                    .UpdateQueryExpression(Visit(shapedQueryExpression.QueryExpression))
                    .UpdateShaperExpression(Visit(shapedQueryExpression.ShaperExpression));

                return shapedQueryExpression.UpdateShaperExpression(Visit(shapedQueryExpression.ShaperExpression));

            case SelectExpression selectExpression:
            {
                TableExpressionBase[]? newTables = null;
                Dictionary<(string, string), ColumnInfo>? columnsToRewrite = null;

                for (var i = 0; i < selectExpression.Tables.Count; i++)
                {
                    var table = selectExpression.Tables[i];

                    if (table.UnwrapJoin() is SqlServerOpenJsonExpression { ColumnInfos: { } columnInfos } openJsonExpression
                        // Condition 1: an ordering/projection still refers to the OPENJSON's [key] column - it needs to be preserved.
                        && (selectExpression.Orderings.Select(o => o.Expression)
                                .Concat(selectExpression.Projection.Select(p => p.Expression))
                                .Any(x => IsKeyColumn(x, openJsonExpression.Alias))
                            ||
                            // Condition 2: a column type in the WITH clause is a SQL Server "CLR type" (e.g. hierarchy id).
                            // These are not supported by OPENJSON with WITH.
                            columnInfos.Any(c => c.TypeMapping.StoreType is "hierarchyid")))
                    {
                        // Remove the WITH clause from the OPENJSON expression
                        var newOpenJsonExpression = openJsonExpression.Update(
                            openJsonExpression.JsonExpression,
                            openJsonExpression.Path,
                            columnInfos: null);

                        table = table switch
                        {
                            JoinExpressionBase j => j.Update(newOpenJsonExpression),
                            SqlServerOpenJsonExpression => newOpenJsonExpression,
                            _ => throw new UnreachableException()
                        };

                        foreach (var columnInfo in columnInfos)
                        {
                            columnsToRewrite ??= new Dictionary<(string, string), ColumnInfo>();
                            columnsToRewrite.Add((newOpenJsonExpression.Alias, columnInfo.Name), columnInfo);
                        }

                        if (newTables is null)
                        {
                            newTables = new TableExpressionBase[selectExpression.Tables.Count];
                            for (var j = 0; j < i; j++)
                            {
                                newTables[j] = selectExpression.Tables[j];
                            }
                        }
                    }

                    if (newTables is not null)
                    {
                        newTables[i] = table;
                    }
                }

                var result = selectExpression;

                // In the common case, we do not have to rewrite any OPENJSON tables.
                if (columnsToRewrite is null)
                {
                    Check.DebugAssert(newTables is null, "newTables must be null if columnsToRewrite is null");

                    result = (SelectExpression)base.Visit(result);
                }
                else
                {
                    if (newTables is not null)
                    {
                        result = selectExpression.Update(
                            newTables,
                            selectExpression.Predicate,
                            selectExpression.GroupBy,
                            selectExpression.Having,
                            selectExpression.Projection,
                            selectExpression.Orderings,
                            selectExpression.Offset,
                            selectExpression.Limit);
                    }

                    // when we mark columns for rewrite we don't yet have the updated SelectExpression, so we store the info in temporary dictionary
                    // and now that we have created new SelectExpression we add it to the proper dictionary that we will use for rewrite
                    foreach (var columnToRewrite in columnsToRewrite)
                    {
                        _columnsToRewrite.Add(columnToRewrite.Key, columnToRewrite.Value);
                    }

                    // Record the OPENJSON expression and its projected column(s), along with the store type we just removed from the WITH
                    // clause. Then visit the select expression, adding a cast around the matching ColumnExpressions.
                    result = (SelectExpression)base.Visit(result);

                    foreach (var columnsToRewriteKey in columnsToRewrite.Keys)
                    {
                        _columnsToRewrite.Remove(columnsToRewriteKey);
                    }
                }

                if (_openjsonOuterAppliesToAdd.Count > 0)
                {
                    result = result.Update(
                        [.. result.Tables, .. _openjsonOuterAppliesToAdd],
                        result.Predicate,
                        result.GroupBy,
                        result.Having,
                        result.Projection,
                        result.Orderings,
                        result.Offset,
                        result.Limit);

                    _openjsonOuterAppliesToAdd.Clear();
                }

                return result;
            }

            case ColumnExpression columnExpression
                when _columnsToRewrite.TryGetValue((columnExpression.TableAlias, columnExpression.Name), out var columnInfo):
            {
                return RewriteOpenJsonColumn(columnExpression, columnInfo);
            }

            // JsonScalarExpression over a column coming out of OPENJSON/WITH; this means that the column represents an owned sub-
            // entity, and therefore must have AS JSON. Rewrite the column and simply collapse the paths together.
            case JsonScalarExpression { Json: ColumnExpression columnExpression } jsonScalarExpression
                when _columnsToRewrite.TryGetValue((columnExpression.TableAlias, columnExpression.Name), out var columnInfo):
            {
                Check.DebugAssert(
                    columnInfo.AsJson,
                    "JsonScalarExpression over a column coming out of OPENJSON is only valid when that column represents an owned "
                    + "sub-entity, which means it must have AS JSON");

                // The new OPENJSON (without WITH) always projects a `value` column, instead of a properly named column for individual
                // values inside; create a new ColumnExpression with that name.
                SqlExpression rewrittenColumn = new ColumnExpression(
                    "value",
                    columnExpression.TableAlias,
                    columnExpression.Type,
                    _nvarcharMaxTypeMapping,
                    columnExpression.IsNullable);

                // Prepend the path from the OPENJSON/WITH to the path in the JsonScalarExpression
                var path = columnInfo.Path is null
                    ? jsonScalarExpression.Path
                    : columnInfo.Path.Concat(jsonScalarExpression.Path).ToList();

                return new JsonScalarExpression(
                    rewrittenColumn,
                    path,
                    jsonScalarExpression.Type,
                    jsonScalarExpression.TypeMapping,
                    jsonScalarExpression.IsNullable);
            }

            // Some SQL Server types cannot be reliably parsed with JSON_VALUE(): binary/varbinary are encoded in base64 in the JSON,
            // but JSON_VALUE() returns a string and there's no SQL Server function to parse base64. However, OPENJSON/WITH does do base64
            // decoding.
            // So here we identify problematic JsonScalarExpressions (which translate to JSON_VALUE), and transform them to OUTER APPLY:
            // JSON_VALUE([b].[Json], '$.Foo.Bar') -> CROSS APPLY OPENJSON([b].[Json]) WITH ([Bar] int '$.Foo.Bar') AS [b].
            case JsonScalarExpression { TypeMapping.StoreTypeNameBase: "varbinary" or "binary" } jsonScalar:
            {
                var name = jsonScalar.Path.LastOrDefault(ps => ps.PropertyName is not null).PropertyName
                    ?? (jsonScalar.Json as ColumnExpression)?.Name
                    ?? "Json";

                // We need to generate an alias here; we always have a manager except
                // when called from SqlNullabilityProcessor (where there's no manager),
                // but in that scenario we never have to deal with JsonScalarExpression,
                // only OpenJsonExpression.
                Check.DebugAssert(sqlAliasManager is not null);
                var tableAlias = sqlAliasManager.GenerateTableAlias(name);
                var join =
                    new OuterApplyExpression(
                        new SqlServerOpenJsonExpression(
                            tableAlias,
                            jsonScalar.Json,
                            path: null,
                            columnInfos: [new ColumnInfo(name, jsonScalar.TypeMapping, jsonScalar.Path)]));

                // We record the new OUTER APPLY in _openWithOuterAppliesToAdd (it gets added after visiting the SelectExpression above),
                // and return a ColumnExpression referencing that new OUTER APPLY.
                _openjsonOuterAppliesToAdd.Add(join);
                return new ColumnExpression(
                    name,
                    tableAlias,
                    jsonScalar.Type,
                    jsonScalar.TypeMapping,
                    jsonScalar.IsNullable);
            }

            // The SQL Server json type cannot be compared ("The JSON data type cannot be compared or sorted, except when using the
            // IS NULL operator"). So we find comparisons that involve the json type, and apply a conversion to string (nvarchar(max))
            // to both sides. We exempt this when one of the sides is a constant null (not required).
            case SqlBinaryExpression
            {
                OperatorType: ExpressionType.Equal or ExpressionType.NotEqual,
                Left: var left,
                Right: var right
            } comparison
                when (left.TypeMapping?.StoreType is "json" || right.TypeMapping?.StoreType is "json")
                    && left is not SqlConstantExpression { Value: null } && right is not SqlConstantExpression { Value: null }:
            {
                return comparison.Update(
                    sqlExpressionFactory.Convert(
                        left,
                        typeof(string),
                        typeMappingSource.FindMapping(typeof(string))),
                    sqlExpressionFactory.Convert(
                        right,
                        typeof(string),
                        typeMappingSource.FindMapping(typeof(string))));
            }

            default:
                return base.Visit(expression);
        }

        static bool IsKeyColumn(SqlExpression sqlExpression, string openJsonTableAlias)
            => (sqlExpression is ColumnExpression { Name: "key", TableAlias: var tableAlias } && tableAlias == openJsonTableAlias)
                || (sqlExpression is SqlUnaryExpression
                    {
                        OperatorType: ExpressionType.Convert,
                        Operand: SqlExpression operand
                    }
                    && IsKeyColumn(operand, openJsonTableAlias));

        SqlExpression RewriteOpenJsonColumn(ColumnExpression columnExpression, ColumnInfo columnInfo)
        {
            // We found a ColumnExpression that refers to the OPENJSON table, we need to rewrite it.

            // Binary data (varbinary) is stored in JSON as base64, which OPENJSON knows how to decode as long the type is
            // specified in the WITH clause. We're now removing the WITH and applying a relational CAST, but that doesn't work
            // for base64 data.
            if (columnInfo.TypeMapping is SqlServerByteArrayTypeMapping)
            {
                throw new InvalidOperationException(SqlServerStrings.QueryingOrderedBinaryJsonCollectionsNotSupported);
            }

            // The new OPENJSON (without WITH) always projects a `value` column, instead of a properly named column for individual
            // values inside; create a new ColumnExpression with that name.
            SqlExpression rewrittenColumn = new ColumnExpression(
                "value", columnExpression.TableAlias, columnExpression.Type, _nvarcharMaxTypeMapping, columnExpression.IsNullable);

            Check.DebugAssert(columnInfo.Path is not null, "Path shouldn't be null in OPENJSON WITH");
            //Check.DebugAssert(
            //    !columnInfo.AsJson || columnInfo.TypeMapping.ElementTypeMapping is not null,
            //    "AS JSON signifies an owned sub-entity or array of primitives being projected out of OPENJSON/WITH. "
            //    + "Columns referring to sub-entities must be wrapped in Json{Scalar,Query}Expression and will have been already dealt with above");

            if (columnInfo.Path is [])
            {
                // OPENJSON with WITH specified the store type in the WITH, but the version without just always projects
                // nvarchar(max); add a CAST to convert.
                if (columnInfo.TypeMapping.StoreType != "nvarchar(max)")
                {
                    _nvarcharMaxTypeMapping ??= typeMappingSource.FindMapping("nvarchar(max)");

                    rewrittenColumn = sqlExpressionFactory.Convert(
                        rewrittenColumn,
                        columnExpression.Type,
                        columnInfo.TypeMapping);
                }
            }
            else
            {
                // Non-primitive collection case - elements in the JSON collection represent a structural type.
                // We need JSON_VALUE to get the individual properties out of those fragments. Note that the appropriate CASTs are added
                // in SQL generation.
                rewrittenColumn = new JsonScalarExpression(
                    rewrittenColumn,
                    columnInfo.Path,
                    columnExpression.Type,
                    columnExpression.TypeMapping,
                    columnExpression.IsNullable);
            }

            return rewrittenColumn;
        }
    }
}
