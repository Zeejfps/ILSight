using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class BuildChildrenHandlerTests
{
    [Fact]
    public async Task HandleAsync_Ended_TheChildrenMayBeAskedForAgain()
    {
        var tree = new BrowseTree { Session = TestSession.Shared };
        var root = new AssemblyList().Open(AppStateDriver.FixturePath).Root;
        tree.Builds.Ask(root);
        var asked = tree.Builds.TakeAsked();
        var ports = new FakeBuildChildrenHandlerPorts();

        await new BuildChildrenHandler(ports).HandleAsync(tree, root, CancellationToken.None);
        tree.Builds.Ask(root);

        Assert.Equal([root], asked);
        Assert.Equal([root], ports.Asked);
        Assert.Equal([root], tree.Builds.TakeAsked());
    }

    [Fact]
    public void Ask_WhileAskedOrBuilding_AsksOnce()
    {
        var tree = new BrowseTree { Session = TestSession.Shared };
        var root = new AssemblyList().Open(AppStateDriver.FixturePath).Root;

        tree.Builds.Ask(root);
        tree.Builds.Ask(root);
        var asked = tree.Builds.TakeAsked();
        tree.Builds.Ask(root);

        Assert.Equal([root], asked);
        Assert.False(tree.Builds.HasAsked);
    }
}
