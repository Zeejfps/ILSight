namespace ILSight;

/// <summary>The nodes the tabs reopened from the open list's saved documents wait for, to be found once its
/// assemblies are listed (<see cref="ShowReopenedNodesHandler"/>): work the workflow that listed them hands back for
/// the edge to start, as nothing after it needs them found (<see cref="TabLoads"/>).</summary>
[MustUse]
public sealed class ReopenedNodesLookup
{
    public required IReadOnlyList<WaitingNode> Nodes { get; init; }
}
