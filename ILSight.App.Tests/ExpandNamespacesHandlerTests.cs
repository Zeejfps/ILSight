using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class ExpandNamespacesHandlerTests
{
    private static async Task<(BrowseTree Tree, BrowseNode Root)> OpenedTreeAsync()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var assembly = list.Open(AppStateDriver.FixturePath);
        var root = await assembly.ReadRootAsync();
        var tree = new BrowseTree { Session = TestSession.Shared };
        tree.TrySyncRoots(list);
        return (tree, root);
    }

    private static async Task<ChildrenState> BuiltAsync(BrowseNode root)
    {
        var rootChildren = await root.ListChildrenAsync();
        return new ChildrenState.Ready(rootChildren);
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
        var fixturesNamespace = await root.GetChildAsync(AppStateDriver.FixturesNamespace);
        Assert.True(tree.View.IsExpanded(fixturesNamespace));
        var greeter = await root.GetChildAsync(AppStateDriver.FixturesNamespace, "Greeter");
        Assert.False(tree.View.IsExpanded(greeter));
    }

    [Fact]
    public async Task HandleAsync_CollapsedMeanwhile_ExpandsNothing()
    {
        var (tree, root) = await OpenedTreeAsync();
        var expansion = tree.ExpandNamespaces();
        tree.CollapseAll();
        var ports = new FakeExpandNamespacesHandlerPorts { Answer = await BuiltAsync(root) };

        await new ExpandNamespacesHandler(ports).HandleAsync(tree, expansion, root, CancellationToken.None);

        var fixturesNamespace = await root.GetChildAsync(AppStateDriver.FixturesNamespace);
        Assert.False(tree.View.IsExpanded(fixturesNamespace));
    }

    [Fact]
    public async Task HandleAsync_NotBuilt_ExpandsNothing()
    {
        var (tree, root) = await OpenedTreeAsync();
        var expansion = tree.ExpandNamespaces();
        var ports = new FakeExpandNamespacesHandlerPorts { Answer = new ChildrenState.Failed(new InvalidDataException("unreadable")) };

        await new ExpandNamespacesHandler(ports).HandleAsync(tree, expansion, root, CancellationToken.None);

        Assert.True(tree.View.IsExpanded(root));
        var fixturesNamespace = await root.GetChildAsync(AppStateDriver.FixturesNamespace);
        Assert.False(tree.View.IsExpanded(fixturesNamespace));
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
        var fixturesNamespace = await root.GetChildAsync(AppStateDriver.FixturesNamespace);
        Assert.False(tree.View.IsExpanded(fixturesNamespace));
    }
}
