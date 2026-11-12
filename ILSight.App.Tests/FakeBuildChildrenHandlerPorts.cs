using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Builds nothing; keeps the nodes it was asked to build.</summary>
internal sealed class FakeBuildChildrenHandlerPorts : BuildChildrenHandler.IPorts
{
    public List<BrowseNode> Asked { get; } = [];

    public Task BuildChildrenAsync(BrowseNode node, CancellationToken ct)
    {
        Asked.Add(node);
        return Task.CompletedTask;
    }
}
