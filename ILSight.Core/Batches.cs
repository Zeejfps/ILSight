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
    public static async IAsyncEnumerable<IReadOnlyList<T>> StreamAsync<T>(Action<Action<T>, CancellationToken> produce,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = stop.Token;
        var producing = Task.Run(() =>
        {
            try
            {
                produce(item => channel.Writer.TryWrite(item), token);
                channel.Writer.TryComplete();
            }
            catch (Exception e)
            {
                channel.Writer.TryComplete(e);
            }
        }, CancellationToken.None);

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
            stop.Cancel();
            await producing.ConfigureAwait(false);
        }
    }
}
