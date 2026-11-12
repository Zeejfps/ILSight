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
    public async Task The_caller_that_starts_a_build_awaits_it_to_its_end_even_when_cancelled()
    {
        var session = new BrowseSession();
        var root = BrowseSessions.EnsureRoot(session, new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.TestAssemblyPath));

        var started = await BrowseSessions.GetChildrenAsync(session, root, new CancellationToken(canceled: true));
        var state = await BrowseSessions.GetChildrenAsync(session, root);

        Assert.NotEmpty(Assert.IsType<ChildrenState.Ready>(started).Children);
        Assert.Same(started, state);
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

        Assert.Same(first.EnsureRoot(assembly), first.EnsureRoot(assembly));
        Assert.Same(first.EnsureRoot(assembly), BrowseSessions.EnsureRoot(first, assembly));
        Assert.NotSame(first.EnsureRoot(assembly), second.EnsureRoot(assembly));
        Assert.IsType<ChildrenState.Unloaded>(second.PeekChildren(first.EnsureRoot(assembly)));
    }

    [Fact]
    public void A_listed_items_root_shows_what_a_node_made_from_its_subject_shows()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var module = Assert.IsType<LoadedModule>(list.Open(Trees.TestAssemblyPath));
        var failed = Assert.IsType<FailedAssembly>(list.Open(_temp.Write("broken.dll", "no assembly")));
        var session = new BrowseSession();

        AssertShowsTheSame(BrowseNodes.Create(null, new NodeSubject.Module { Value = module }), session.EnsureRoot(module));
        AssertShowsTheSame(BrowseNodes.Create(null, new NodeSubject.FailedAssembly { Value = failed }), session.EnsureRoot(failed));
    }

    private static void AssertShowsTheSame(BrowseNode expected, BrowseNode actual)
    {
        Assert.Equal(expected.Path, actual.Path);
        Assert.Equal(expected.Text, actual.Text);
        Assert.Same(expected.Assembly, actual.Assembly);
        Assert.Equal(expected.Subject.GetType(), actual.Subject.GetType());
        Assert.Equal(expected.FullName, actual.FullName);
        Assert.Equal(expected.Visibility, actual.Visibility);
        Assert.Equal(expected.Reference, actual.Reference);
        Assert.Equal(expected.Activation, actual.Activation);
        Assert.Equal(expected.Analysis, actual.Analysis);
        Assert.Equal(expected.CanHaveChildren, actual.CanHaveChildren);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.IsCode, actual.IsCode);
        Assert.Equal(expected.ResourceFileName, actual.ResourceFileName);
    }
}
