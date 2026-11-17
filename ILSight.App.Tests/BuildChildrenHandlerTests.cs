using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class BuildChildrenHandlerTests
{
    [Fact]
    public async Task HandleAsync_Ended_TheChildrenMayBeAskedForAgain()
    {
        var tree = new BrowseTree { Session = TestSession.Shared };
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath);
        var root = await assembly.ReadRootAsync();
        tree.Builds.Ask(root);
        var firstAsked = tree.Builds.TakeAsked();
        var ports = new FakeBuildChildrenHandlerPorts();

        await new BuildChildrenHandler(ports).HandleAsync(tree, root, CancellationToken.None);
        tree.Builds.Ask(root);

        Assert.Equal([root], firstAsked);
        Assert.Equal([root], ports.Asked);
        var secondAsked = tree.Builds.TakeAsked();
        Assert.Equal([root], secondAsked);
    }

    [Fact]
    public async Task Ask_WhileAskedOrBuilding_AsksOnce()
    {
        var tree = new BrowseTree { Session = TestSession.Shared };
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath);
        var root = await assembly.ReadRootAsync();

        tree.Builds.Ask(root);
        tree.Builds.Ask(root);
        var asked = tree.Builds.TakeAsked();
        tree.Builds.Ask(root);

        Assert.Equal([root], asked);
        Assert.False(tree.Builds.HasAsked);
    }
}
