using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class ExpandNamespacesHandlerTests
{
    private static async Task<(BrowseTree Tree, BrowseNode Root)> OpenedTreeAsync()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var root = await list.Open(AppStateDriver.FixturePath).ReadRootAsync();
        var tree = new BrowseTree { Session = TestSession.Shared };
        tree.TrySyncRoots(list);
        return (tree, root);
    }

    private static async Task<ChildrenState> BuiltAsync(BrowseNode root)
    {
        return new ChildrenState.Ready(await root.ListChildrenAsync());
    }

    [Fact]
    public async Task HandleAsync_Built_ExpandsTheNamespacesOnly()
    {
        var (tree, root) = await OpenedTreeAsync();
        var expansion = tree.ExpandNamespaces();
        var ports = new FakeExpandNamespacesHandlerPorts { Answer = await BuiltAsync(root) };

        await new ExpandNamespacesHandler(ports).HandleAsync(tree, expansion, root, CancellationToken.None);

        Assert.Equal([root], ports.Asked);
        Assert.True(tree.View.IsExpanded(root));
        Assert.True(tree.View.IsExpanded(await root.GetChildAsync(AppStateDriver.FixturesNamespace)));
        Assert.False(tree.View.IsExpanded(await root.GetChildAsync(AppStateDriver.FixturesNamespace, "Greeter")));
    }

    [Fact]
    public async Task HandleAsync_CollapsedMeanwhile_ExpandsNothing()
    {
        var (tree, root) = await OpenedTreeAsync();
        var expansion = tree.ExpandNamespaces();
        tree.CollapseAll();
        var ports = new FakeExpandNamespacesHandlerPorts { Answer = await BuiltAsync(root) };

        await new ExpandNamespacesHandler(ports).HandleAsync(tree, expansion, root, CancellationToken.None);

        Assert.False(tree.View.IsExpanded(await root.GetChildAsync(AppStateDriver.FixturesNamespace)));
    }

    [Fact]
    public async Task HandleAsync_NotBuilt_ExpandsNothing()
    {
        var (tree, root) = await OpenedTreeAsync();
        var expansion = tree.ExpandNamespaces();
        var ports = new FakeExpandNamespacesHandlerPorts { Answer = new ChildrenState.Failed(new InvalidDataException("unreadable")) };

        await new ExpandNamespacesHandler(ports).HandleAsync(tree, expansion, root, CancellationToken.None);

        Assert.True(tree.View.IsExpanded(root));
        Assert.False(tree.View.IsExpanded(await root.GetChildAsync(AppStateDriver.FixturesNamespace)));
    }

    [Fact]
    public async Task HandleAsync_BuildThrows_LetsItThroughToTheEdge()
    {
        var (tree, root) = await OpenedTreeAsync();
        var expansion = tree.ExpandNamespaces();
        var bug = new InvalidOperationException("A bug in the core's own code on what it read.");
        var ports = new FakeExpandNamespacesHandlerPorts { Throws = bug };

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => new ExpandNamespacesHandler(ports).HandleAsync(tree, expansion, root, CancellationToken.None));

        Assert.Same(bug, thrown);
        Assert.False(tree.View.IsExpanded(await root.GetChildAsync(AppStateDriver.FixturesNamespace)));
    }
}
