namespace ILSight.App.Tests;

/// <summary>The bridge from the app tests' synchronous queries (<see cref="TreeQueries"/>: the tree read as properties,
/// <c>node.Children</c>, <c>assembly.Root</c>, chained in assertions) to the async reads they stand for: the read runs
/// on the thread pool, so no test's context holds its continuations, and is waited for. An app test's thread has the
/// app's UI context installed (<see cref="PostedActions"/>), so an <c>await</c> there continues only when the test runs
/// the app's frames; the tests of the core await their reads instead. The one place the tests block on a task.</summary>
internal static class TestWaits
{
    public static T Wait<T>(Func<Task<T>> read)
    {
        return Task.Run(read).GetAwaiter().GetResult();
    }
}
