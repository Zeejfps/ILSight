using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Builds nothing: answers every build as the test says; keeps the nodes it was asked to build.</summary>
internal sealed class FakeExpandNamespacesHandlerPorts : ExpandNamespacesHandler.IPorts
{
    public required Outcome<ChildrenState> Answer { get; init; }

    public List<BrowseNode> Asked { get; } = [];

    public Task<Outcome<ChildrenState>> BuildAssemblyChildrenAsync(BrowseNode assembly, CancellationToken ct)
    {
        Asked.Add(assembly);
        return Task.FromResult(Answer);
    }
}
