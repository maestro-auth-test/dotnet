﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Shared.Extensions;

internal static class AsyncEnumerableFactory
{
    internal static class AsyncEnumerable<T>
    {
        public static readonly IAsyncEnumerable<T> Empty = GetEmptyAsync();

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private static async IAsyncEnumerable<T> GetEmptyAsync()
        {
            yield break;
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    internal static async IAsyncEnumerable<T> SingletonAsync<T>(T value)
    {
        yield return value;
    }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    /// <summary>
    /// Takes an array of <see cref="IAsyncEnumerable{T}"/>s and produces a single resultant <see
    /// cref="IAsyncEnumerable{T}"/> with all their values merged together.  Absolutely no ordering guarantee is
    /// provided.  It will be expected that the individual values from distinct enumerables will be interleaved
    /// together.
    /// </summary>
    /// <remarks>This helper is useful when doign parallel processing of work where each job returns an <see
    /// cref="IAsyncEnumerable{T}"/>, but one final stream is desired as the result.</remarks>
    public static IAsyncEnumerable<T> MergeAsync<T>(this ImmutableArray<IAsyncEnumerable<T>> streams, CancellationToken cancellationToken)
    {
        // Code provided by Stephen Toub, but heavily modified after that.

        // 1024 chosen as a way to ensure we don't necessarily create a huge unbounded channel, while also making it
        // so that we're unlikely to throttle on any stream unless there is truly a huge amount of results in it.
        var channel = Channel.CreateBounded<T>(1024);

        var tasks = new Task[streams.Length];
        for (var i = 0; i < streams.Length; i++)
            tasks[i] = ProcessAsync(streams[i], channel.Writer, cancellationToken);

        // Complete the channel writer with the result of all the tasks.  If nothing failed, t.Exception will be
        // null and this will complete successfully.  If anything failed, the exception will propagate out.
        //
        // Note: passing CancellationToken.None here is intentional/correct.  We must complete all the channels to
        // allow reading to complete as well.
        Task.WhenAll(tasks).CompletesChannel(channel);

        return channel.Reader.ReadAllAsync(cancellationToken);

        static async Task ProcessAsync(IAsyncEnumerable<T> stream, ChannelWriter<T> writer, CancellationToken cancellationToken)
        {
            await foreach (var value in stream.ConfigureAwait(false))
                await writer.WriteAsync(value, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs after task completes in any fashion (success, cancellation, faulting) and ensures the channel writer is
    /// always completed.  If the task faults then the exception from that task will be used to complete the channel
    /// </summary>
    public static void CompletesChannel<T>(this Task task, Channel<T> channel)
    {
        // Note: using `Complete(task.Exception)` is always fine.  Exception is only produced in the case of
        // faulting. it is null otherwise.
        task.ContinueWith(
            static (task, channel) => ((Channel<T>)channel!).Writer.Complete(task.Exception),
            channel, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }
}
