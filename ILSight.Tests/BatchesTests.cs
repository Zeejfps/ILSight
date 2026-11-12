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
            return Task.CompletedTask;
        }, CancellationToken.None);

        await foreach (var batch in stream)
        {
            Assert.Equal([1], batch);
            break;
        }

        Assert.True(stopped.IsSet);
    }

    [Fact]
    public async Task A_producer_that_fails_ends_the_stream_with_its_failure_after_what_it_emitted()
    {
        var stream = Batches.StreamAsync<int>(static (emit, _) =>
        {
            emit(1);
            throw new InvalidOperationException("The producer failed.");
        }, CancellationToken.None);
        var received = new List<int>();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var batch in stream)
            {
                received.AddRange(batch);
            }
        });

        Assert.Equal("The producer failed.", failure.Message);
        Assert.Equal([1], received);
    }
}
