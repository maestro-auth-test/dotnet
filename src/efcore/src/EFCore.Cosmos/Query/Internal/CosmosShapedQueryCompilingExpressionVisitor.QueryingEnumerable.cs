// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections;
using System.Text;
using Microsoft.EntityFrameworkCore.Cosmos.Storage.Internal;
using Newtonsoft.Json.Linq;

namespace Microsoft.EntityFrameworkCore.Cosmos.Query.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public partial class CosmosShapedQueryCompilingExpressionVisitor
{
    private sealed class QueryingEnumerable<T> : IEnumerable<T>, IAsyncEnumerable<T>, IQueryingEnumerable
    {
        private readonly CosmosQueryContext _cosmosQueryContext;
        private readonly ISqlExpressionFactory _sqlExpressionFactory;
        private readonly SelectExpression _selectExpression;
        private readonly Func<CosmosQueryContext, JToken, T> _shaper;
        private readonly IQuerySqlGeneratorFactory _querySqlGeneratorFactory;
        private readonly Type _contextType;
        private readonly string _cosmosContainer;
        private readonly PartitionKey _cosmosPartitionKey;
        private readonly IDiagnosticsLogger<DbLoggerCategory.Query> _queryLogger;
        private readonly bool _standAloneStateManager;
        private readonly bool _threadSafetyChecksEnabled;

        public QueryingEnumerable(
            CosmosQueryContext cosmosQueryContext,
            ISqlExpressionFactory sqlExpressionFactory,
            IQuerySqlGeneratorFactory querySqlGeneratorFactory,
            SelectExpression selectExpression,
            Func<CosmosQueryContext, JToken, T> shaper,
            Type contextType,
            IEntityType rootEntityType,
            List<Expression> partitionKeyPropertyValues,
            bool standAloneStateManager,
            bool threadSafetyChecksEnabled)
        {
            _cosmosQueryContext = cosmosQueryContext;
            _sqlExpressionFactory = sqlExpressionFactory;
            _querySqlGeneratorFactory = querySqlGeneratorFactory;
            _selectExpression = selectExpression;
            _shaper = shaper;
            _contextType = contextType;
            _queryLogger = cosmosQueryContext.QueryLogger;
            _standAloneStateManager = standAloneStateManager;
            _threadSafetyChecksEnabled = threadSafetyChecksEnabled;

            _cosmosContainer = rootEntityType.GetContainer()
                ?? throw new UnreachableException("Root entity type without a Cosmos container.");
            _cosmosPartitionKey = GeneratePartitionKey(
                rootEntityType, partitionKeyPropertyValues, _cosmosQueryContext.Parameters);
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new AsyncEnumerator(this, cancellationToken);

        public IEnumerator<T> GetEnumerator()
            => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        private CosmosSqlQuery GenerateQuery()
            => _querySqlGeneratorFactory.Create().GetSqlQuery(
                (SelectExpression)new ParameterInliner(
                        _sqlExpressionFactory,
                        _cosmosQueryContext.Parameters)
                    .Visit(_selectExpression),
                _cosmosQueryContext.Parameters);

        public string ToQueryString()
        {
            var sqlQuery = GenerateQuery();
            if (sqlQuery.Parameters.Count == 0)
            {
                return sqlQuery.Query;
            }

            var builder = new StringBuilder();
            foreach (var parameter in sqlQuery.Parameters)
            {
                builder
                    .Append("-- ")
                    .Append(parameter.Name)
                    .Append("='")
                    .Append(parameter.Value)
                    .AppendLine("'");
            }

            return builder.Append(sqlQuery.Query).ToString();
        }

        private sealed class Enumerator : IEnumerator<T>
        {
            private readonly QueryingEnumerable<T> _queryingEnumerable;
            private readonly CosmosQueryContext _cosmosQueryContext;
            private readonly Func<CosmosQueryContext, JToken, T> _shaper;
            private readonly Type _contextType;
            private readonly string _cosmosContainer;
            private readonly PartitionKey _cosmosPartitionKey;
            private readonly IDiagnosticsLogger<DbLoggerCategory.Query> _queryLogger;
            private readonly bool _standAloneStateManager;
            private readonly IConcurrencyDetector _concurrencyDetector;
            private readonly IExceptionDetector _exceptionDetector;

            private IEnumerator<JToken> _enumerator;

            public Enumerator(QueryingEnumerable<T> queryingEnumerable)
            {
                _queryingEnumerable = queryingEnumerable;
                _cosmosQueryContext = queryingEnumerable._cosmosQueryContext;
                _shaper = queryingEnumerable._shaper;
                _contextType = queryingEnumerable._contextType;
                _cosmosContainer = queryingEnumerable._cosmosContainer;
                _cosmosPartitionKey = queryingEnumerable._cosmosPartitionKey;
                _queryLogger = queryingEnumerable._queryLogger;
                _standAloneStateManager = queryingEnumerable._standAloneStateManager;
                _exceptionDetector = _cosmosQueryContext.ExceptionDetector;

                _concurrencyDetector = queryingEnumerable._threadSafetyChecksEnabled
                    ? _cosmosQueryContext.ConcurrencyDetector
                    : null;
            }

            public T Current { get; private set; }

            object IEnumerator.Current
                => Current;

            public bool MoveNext()
            {
                try
                {
                    using var _ = _concurrencyDetector?.EnterCriticalSection();

                    if (_enumerator == null)
                    {
                        var sqlQuery = _queryingEnumerable.GenerateQuery();

                        EntityFrameworkMetricsData.ReportQueryExecuting();

                        _enumerator = _cosmosQueryContext.CosmosClient
                            .ExecuteSqlQuery(_cosmosContainer, _cosmosPartitionKey, sqlQuery)
                            .GetEnumerator();
                        _cosmosQueryContext.InitializeStateManager(_standAloneStateManager);
                    }

                    var hasNext = _enumerator.MoveNext();

                    Current
                        = hasNext
                            ? _shaper(_cosmosQueryContext, _enumerator.Current)
                            : default;

                    return hasNext;
                }
                catch (Exception exception)
                {
                    if (_exceptionDetector.IsCancellation(exception))
                    {
                        _queryLogger.QueryCanceled(_contextType);
                    }
                    else
                    {
                        _queryLogger.QueryIterationFailed(_contextType, exception);
                    }

                    throw;
                }
            }

            public void Dispose()
            {
                _enumerator?.Dispose();
                _enumerator = null;
            }

            public void Reset()
                => throw new NotSupportedException(CoreStrings.EnumerableResetNotSupported);
        }

        private sealed class AsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly QueryingEnumerable<T> _queryingEnumerable;
            private readonly CosmosQueryContext _cosmosQueryContext;
            private readonly Func<CosmosQueryContext, JToken, T> _shaper;
            private readonly Type _contextType;
            private readonly string _cosmosContainer;
            private readonly PartitionKey _cosmosPartitionKey;
            private readonly IDiagnosticsLogger<DbLoggerCategory.Query> _queryLogger;
            private readonly bool _standAloneStateManager;
            private readonly CancellationToken _cancellationToken;
            private readonly IConcurrencyDetector _concurrencyDetector;
            private readonly IExceptionDetector _exceptionDetector;

            private IAsyncEnumerator<JToken> _enumerator;

            public AsyncEnumerator(QueryingEnumerable<T> queryingEnumerable, CancellationToken cancellationToken)
            {
                _queryingEnumerable = queryingEnumerable;
                _cosmosQueryContext = queryingEnumerable._cosmosQueryContext;
                _shaper = queryingEnumerable._shaper;
                _contextType = queryingEnumerable._contextType;
                _cosmosContainer = queryingEnumerable._cosmosContainer;
                _cosmosPartitionKey = queryingEnumerable._cosmosPartitionKey;
                _queryLogger = queryingEnumerable._queryLogger;
                _standAloneStateManager = queryingEnumerable._standAloneStateManager;
                _exceptionDetector = _cosmosQueryContext.ExceptionDetector;
                _cancellationToken = cancellationToken;

                _concurrencyDetector = queryingEnumerable._threadSafetyChecksEnabled
                    ? _cosmosQueryContext.ConcurrencyDetector
                    : null;
            }

            public T Current { get; private set; }

            public async ValueTask<bool> MoveNextAsync()
            {
                try
                {
                    using var _ = _concurrencyDetector?.EnterCriticalSection();

                    if (_enumerator == null)
                    {
                        var sqlQuery = _queryingEnumerable.GenerateQuery();

                        EntityFrameworkMetricsData.ReportQueryExecuting();

                        _enumerator = _cosmosQueryContext.CosmosClient
                            .ExecuteSqlQueryAsync(_cosmosContainer, _cosmosPartitionKey, sqlQuery)
                            .GetAsyncEnumerator(_cancellationToken);
                        _cosmosQueryContext.InitializeStateManager(_standAloneStateManager);
                    }

                    var hasNext = await _enumerator.MoveNextAsync().ConfigureAwait(false);

                    Current
                        = hasNext
                            ? _shaper(_cosmosQueryContext, _enumerator.Current)
                            : default;

                    return hasNext;
                }
                catch (Exception exception)
                {
                    if (_exceptionDetector.IsCancellation(exception, _cancellationToken))
                    {
                        _queryLogger.QueryCanceled(_contextType);
                    }
                    else
                    {
                        _queryLogger.QueryIterationFailed(_contextType, exception);
                    }

                    throw;
                }
            }

            public ValueTask DisposeAsync()
            {
                var enumerator = _enumerator;
                if (enumerator != null)
                {
                    _enumerator = null;
                    return enumerator.DisposeAsync();
                }

                return default;
            }
        }
    }
}
