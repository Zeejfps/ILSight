using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class ExpandNamespacesHandlerTests
{
    private static (BrowseTree Tree, BrowseNode Root) OpenedTree()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var root = list.Open(AppStateDriver.FixturePath).Root;
        var tree = new BrowseTree { Session = TestSession.Shared };
        tree.TrySyncRoots(list);
        return (tree, root);
    }

    private static Outcome<ChildrenState> Built(BrowseNode root)
    {
        return new Outcome<ChildrenState>.Ok(new ChildrenState.Ready(root.Children));
    }

    [Fact]
    public async Task HandleAsync_Built_ExpandsTheNamespacesOnly()
    {
        var (tree, root) = OpenedTree();
        var expansion = tree.ExpandNamespaces();
        var ports = new FakeExpandNamespacesHandlerPorts { Answer = Built(root) };

        await new ExpandNamespacesHandler(ports).HandleAsync(tree, expansion, root, CancellationToken.None);

        Assert.Equal([root], ports.Asked);
        Assert.True(tree.View.IsExpanded(root));
        Assert.True(tree.View.IsExpanded(root.Child(AppStateDriver.FixturesNamespace)));
        Assert.False(tree.View.IsExpanded(root.Child(AppStateDriver.FixturesNamespace, "Greeter")));
    }

    [Fact]
    public async Task HandleAsync_CollapsedMeanwhile_ExpandsNothing()
    {
        var (tree, root) = OpenedTree();
        var expansion = tree.ExpandNamespaces();
        tree.CollapseAll();
        var ports = new FakeExpandNamespacesHandlerPorts { Answer = Built(root) };

        await new ExpandNamespacesHandler(ports).HandleAsync(tree, expansion, root, CancellationToken.None);

        Assert.False(tree.View.IsExpanded(root.Child(AppStateDriver.FixturesNamespace)));
    }

    [Fact]
    public async Task HandleAsync_NotBuilt_ExpandsNothing()
    {
        var (tree, root) = OpenedTree();
        var expansion = tree.ExpandNamespaces();
        var ports = new FakeExpandNamespacesHandlerPorts { Answer = new Outcome<ChildrenState>.Failed(new InvalidDataException("unreadable")) };

        await new ExpandNamespacesHandler(ports).HandleAsync(tree, expansion, root, CancellationToken.None);

        Assert.True(tree.View.IsExpanded(root));
        Assert.False(tree.View.IsExpanded(root.Child(AppStateDriver.FixturesNamespace)));
    }
}
