namespace ILSight.Tests;

/// <summary>The bridge from the tests' synchronous queries (the tree read as properties: <c>node.Children</c>,
/// <c>assembly.Root</c>, chained in assertions) to the async reads they stand for: the read runs on the thread pool, so
/// no test's context holds its continuations, and is waited for. The one place the tests block on a task.</summary>
internal static class TestWaits
{
    public static T Wait<T>(Func<Task<T>> read)
    {
        return Task.Run(read).GetAwaiter().GetResult();
    }
}
