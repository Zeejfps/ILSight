using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Builds nothing: answers every build as the test says, or throws what it says; keeps the nodes it was asked
/// to build.</summary>
internal sealed class FakeExpandNamespacesHandlerPorts : ExpandNamespacesHandler.IPorts
{
    public ChildrenState Answer { get; init; } = ChildrenState.NotLoaded;

    public Exception? Throws { get; init; }

    public List<BrowseNode> Asked { get; } = [];

    public Task<ChildrenState> BuildAssemblyChildrenAsync(BrowseNode assembly, CancellationToken ct)
    {
        Asked.Add(assembly);
        if (Throws is not null)
        {
            return Task.FromException<ChildrenState>(Throws);
        }

        return Task.FromResult(Answer);
    }
}
