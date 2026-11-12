using ILSight.Core;

namespace ILSight.Tests;

public sealed class BatchesTests
{
    [Fact]
    public async Task Leaving_the_stream_early_ends_it_once_its_producer_has_stopped()
    {
        using var stopped = new ManualResetEventSlim();
        var stream = Batches.StreamAsync<int>((emit, token) =>
        {
            emit(1);
            token.WaitHandle.WaitOne();
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
            stopped.Set();
        }, CancellationToken.None);

        await foreach (var batch in stream)
        {
            Assert.Equal([1], batch);
            break;
        }

        Assert.True(stopped.IsSet);
    }
}
