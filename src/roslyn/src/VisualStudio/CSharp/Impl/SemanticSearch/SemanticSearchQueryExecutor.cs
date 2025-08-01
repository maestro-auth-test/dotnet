﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.FindUsages;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.Notification;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.SemanticSearch;

namespace Microsoft.VisualStudio.LanguageServices.CSharp;

internal sealed class SemanticSearchQueryExecutor(
    FindUsagesContext presenterContext,
    IOptionsReader options)
{
    private sealed class ResultsObserver(IFindUsagesContext presenterContext, IOptionsReader options, Document? queryDocument) : ISemanticSearchResultsDefinitionObserver
    {
        public ValueTask<ClassificationOptions> GetClassificationOptionsAsync(Microsoft.CodeAnalysis.Host.LanguageServices language, CancellationToken cancellationToken)
            => new(options.GetClassificationOptions(language.Language));

        public ValueTask OnDefinitionFoundAsync(DefinitionItem definition, CancellationToken cancellationToken)
            => presenterContext.OnDefinitionFoundAsync(definition, cancellationToken);

        public ValueTask AddItemsAsync(int itemCount, CancellationToken cancellationToken)
            => presenterContext.ProgressTracker.AddItemsAsync(itemCount, cancellationToken);

        public ValueTask ItemsCompletedAsync(int itemCount, CancellationToken cancellationToken)
            => presenterContext.ProgressTracker.ItemsCompletedAsync(itemCount, cancellationToken);

        public ValueTask OnUserCodeExceptionAsync(UserCodeExceptionInfo exception, CancellationToken cancellationToken)
            => presenterContext.OnDefinitionFoundAsync(
                new SearchExceptionDefinitionItem(exception.Message, exception.TypeName, exception.StackTrace, (queryDocument != null) ? new DocumentSpan(queryDocument, exception.Span) : default), cancellationToken);
    }

    public async Task ExecuteAsync(string? query, Document? queryDocument, Solution solution, CancellationToken cancellationToken)
    {
        Contract.ThrowIfFalse(query is null ^ queryDocument is null);

        if (solution.ProjectIds is [])
        {
            try
            {
                await presenterContext.ReportNoResultsAsync(ServicesVSResources.Search_found_no_results_no_csharp_or_vb_projects_opened, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Notify the presenter even if the search has been cancelled.
                await presenterContext.OnCompletedAsync(CancellationToken.None).ConfigureAwait(false);
            }

            return;
        }

        var resultsObserver = new ResultsObserver(presenterContext, options, queryDocument);
        query ??= (await queryDocument!.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString();

        ExecuteQueryResult result = default;
        var canceled = false;
        var emitTime = TimeSpan.Zero;

        try
        {
            var compileResult = await RemoteSemanticSearchServiceProxy.CompileQueryAsync(
                solution.Services,
                query,
                cancellationToken).ConfigureAwait(false);

            if (compileResult == null)
            {
                result = new ExecuteQueryResult(FeaturesResources.Semantic_search_only_supported_on_net_core);
                return;
            }

            emitTime = compileResult.Value.EmitTime;

            if (!compileResult.Value.CompilationErrors.IsEmpty)
            {
                foreach (var error in compileResult.Value.CompilationErrors)
                {
                    await presenterContext.OnDefinitionFoundAsync(new SearchCompilationFailureDefinitionItem(error, queryDocument), cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            result = await RemoteSemanticSearchServiceProxy.ExecuteQueryAsync(
                solution,
                compileResult.Value.QueryId,
                resultsObserver,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (FatalError.ReportAndPropagateUnlessCanceled(e, cancellationToken, ErrorSeverity.Critical))
        {
            result = new ExecuteQueryResult(e.Message);
        }
        catch (OperationCanceledException)
        {
            result = new ExecuteQueryResult(ServicesVSResources.Search_cancelled);
            canceled = true;
        }
        finally
        {
            var errorMessage = result.ErrorMessage;

            if (errorMessage != null)
            {
                if (result.ErrorMessageArgs != null)
                {
                    errorMessage = string.Format(errorMessage, result.ErrorMessageArgs);
                }

                // not cancellable since we might be reporting cancellation:
                await presenterContext.ReportMessageAsync(
                    errorMessage,
                    canceled ? NotificationSeverity.Information : NotificationSeverity.Error,
                    CancellationToken.None).ConfigureAwait(false);
            }

            // Notify the presenter even if the search has been cancelled.
            await presenterContext.OnCompletedAsync(CancellationToken.None).ConfigureAwait(false);

            ReportTelemetry(query, result, emitTime, canceled);
        }
    }

    private static void ReportTelemetry(string queryString, ExecuteQueryResult result, TimeSpan emitTime, bool canceled)
    {
        Logger.Log(FunctionId.SemanticSearch_QueryExecution, KeyValueLogMessage.Create(map =>
        {
            map["Query"] = new PiiValue(queryString);

            if (canceled)
            {
                map["Canceled"] = true;
            }
            else if (result.ErrorMessage != null)
            {
                map["ErrorMessage"] = result.ErrorMessage;

                if (result.ErrorMessageArgs != null)
                {
                    map["ErrorMessageArgs"] = new PiiValue(string.Join("|", result.ErrorMessageArgs));
                }
            }

            map["ExecutionTimeMilliseconds"] = (long)result.ExecutionTime.TotalMilliseconds;
            map["EmitTime"] = (long)emitTime.TotalMilliseconds;
        }));
    }
}
