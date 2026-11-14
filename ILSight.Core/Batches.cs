using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ILSight.Core;

internal static class Batches
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    /// <summary>Runs <paramref name="produce"/> on the thread pool and yields what it emits in
    /// batches: all that arrived since the last batch, at most one batch per interval. A failure of
    /// <paramref name="produce"/> ends the stream with it; leaving the stream early cancels it, and
    /// the stream ends once it has stopped.</summary>
    public static async IAsyncEnumerable<IReadOnlyList<T>> StreamAsync<T>(Func<Action<T>, CancellationToken, Task> produce,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = stop.Token;
        var producing = Task.Run(() => produce(item => channel.Writer.TryWrite(item), token), CancellationToken.None);
        // The stream ends when producing does, with what it threw when it failed.
        var ending = producing.ContinueWith(done => channel.Writer.TryComplete(done.Exception?.InnerException), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        try
        {
            while (true)
            {
                var hasItems = await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
                if (!hasItems)
                {
                    break;
                }

                var batch = new List<T>();
                while (channel.Reader.TryRead(out var item))
                {
                    batch.Add(item);
                }

                yield return batch;
                if (!channel.Reader.Completion.IsCompleted)
                {
                    await Task.Delay(Interval, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(false);
            await ending.ConfigureAwait(false);
        }
    }

    /// <summary>As <see cref="StreamAsync{T}"/>, for <paramref name="produce"/> that answers how its read ended: what it
    /// emitted comes in batches, and a read that failed ends the stream with <see cref="Outcome{T}.Failed"/> after
    /// them. What <paramref name="produce"/> throws still ends the stream with it.</summary>
    public static async IAsyncEnumerable<Outcome<IReadOnlyList<T>>> StreamOutcomesAsync<T>(Func<Action<T>, CancellationToken, Task<Outcome>> produce,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Outcome ended = new Outcome.Done();
        var batches = StreamAsync<T>(async (emit, token) => ended = await produce(emit, token), cancellationToken);
        await foreach (var batch in batches.ConfigureAwait(false))
        {
            yield return new Outcome<IReadOnlyList<T>>.Ok(batch);
        }

        if (ended is Outcome.Failed failed)
        {
            yield return new Outcome<IReadOnlyList<T>>.Failed(failed.Error);
        }
    }
}
