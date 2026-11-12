using ILSight.Core;

namespace ILSight.Tests;

public sealed class BrowseSessionTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public void Children_are_built_once_and_every_caller_gets_the_same_ones()
    {
        var session = new BrowseSession();
        var root = BrowseSessions.EnsureRoot(session, new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.TestAssemblyPath));

        var waits = Enumerable.Range(0, 8).Select(_ => BrowseSessions.GetChildrenAsync(session, root)).ToArray();
        var blocking = Enumerable.Range(0, 8).AsParallel().WithDegreeOfParallelism(8).Select(_ => BrowseSessions.GetChildren(session, root)).ToArray();
        var waited = Task.WhenAll(waits).GetAwaiter().GetResult();

        var children = Assert.IsType<ChildrenState.Ready>(session.PeekChildren(root)).Children;
        Assert.All(waited.Concat(blocking), s => Assert.Same(children, Assert.IsType<ChildrenState.Ready>(s).Children));
    }

    [Fact]
    public async Task Cancelling_a_wait_leaves_the_build_running_for_the_others()
    {
        var session = new BrowseSession();
        var root = BrowseSessions.EnsureRoot(session, new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.TestAssemblyPath));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BrowseSessions.GetChildrenAsync(session, root, new CancellationToken(canceled: true)));
        var state = await BrowseSessions.GetChildrenAsync(session, root);

        Assert.NotEmpty(Assert.IsType<ChildrenState.Ready>(state).Children);
    }

    [Fact]
    public void Children_that_fail_to_build_say_why_once_and_count_as_children()
    {
        var session = new BrowseSession();
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(BrokenAssemblies.WriteUnreadableAvaloniaContainer(_temp));
        var resources = BrowseSessions.ListChildren(session, BrowseSessions.EnsureRoot(session, assembly)).Single(c => c.Subject is NodeSubject.Resources);
        var container = Assert.Single(BrowseSessions.ListChildren(session, resources));

        var failed = Assert.IsType<ChildrenState.Failed>(BrowseSessions.GetChildren(session, container));

        Assert.IsType<InvalidDataException>(failed.Error);
        Assert.Same(failed, BrowseSessions.GetChildren(session, container));
        Assert.True(session.HasChildren(container));
        Assert.Empty(BrowseSessions.ListChildren(session, container));
    }

    [Fact]
    public void Each_session_builds_a_tree_of_its_own()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.TestAssemblyPath);
        var first = new BrowseSession();
        var second = new BrowseSession();

        Assert.Same(BrowseSessions.EnsureRoot(first, assembly), BrowseSessions.EnsureRoot(first, assembly));
        Assert.NotSame(BrowseSessions.EnsureRoot(first, assembly), BrowseSessions.EnsureRoot(second, assembly));
        Assert.IsType<ChildrenState.Unloaded>(second.PeekChildren(BrowseSessions.EnsureRoot(first, assembly)));
    }
}
