using ILSight.Core.Processes;

namespace ILSight.Tests;

/// <summary>Reading a rundown from its connection, and winding the collection up however it ended.</summary>
public sealed class RundownCollectionTests
{
    [Fact]
    public async Task A_finished_rundown_hands_out_its_trace_from_the_start_and_closes_the_connection()
    {
        var connection = new HeldConnection();
        MemoryStream trace;
        var collection = RundownCollection.Start(connection, CancellationToken.None);
        await using (collection)
        {
            connection.EndRundown([1, 2, 3]);
            trace = await collection.FinishAsync(CancellationToken.None);
        }

        Assert.True(connection.IsClosed);
        Assert.Null(collection.DrainEnding);
        Assert.Equal(0, trace.Position);
        Assert.Equal([1, 2, 3], trace.ToArray());
    }

    [Fact]
    public async Task A_collection_cancelled_before_the_rundown_ends_closes_the_connection_and_awaits_the_read_it_ended()
    {
        var connection = new HeldConnection();
        using var cancel = new CancellationTokenSource();
        var collection = RundownCollection.Start(connection, cancel.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await using (collection)
            {
                await cancel.CancelAsync();
                await collection.FinishAsync(cancel.Token);
            }
        });

        Assert.True(connection.IsClosed);
        var ending = Assert.IsType<IOException>(collection.DrainEnding);
        Assert.Equal(HeldConnection.ClosedUnderTheRead, ending.Message);
    }

    [Fact]
    public async Task A_collection_wound_up_after_its_trace_was_taken_leaves_the_trace_to_its_taker()
    {
        var connection = new HeldConnection();
        var collection = RundownCollection.Start(connection, CancellationToken.None);
        connection.EndRundown([7]);
        var trace = await collection.FinishAsync(CancellationToken.None);

        await collection.DisposeAsync();

        Assert.True(trace.CanRead);
        Assert.Equal([7], trace.ToArray());
    }
}
