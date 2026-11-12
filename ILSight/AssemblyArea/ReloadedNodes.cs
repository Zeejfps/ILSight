using ILSight.Core;

namespace ILSight.AssemblyArea;

/// <summary>The nodes a reload found again: those the tree had expanded, with their children loaded, and
/// each tab's place, not found where it is gone, unreadable where in a module that could not be read, in the
/// order the places were asked for.</summary>
public sealed record ReloadedNodes
{
    public required List<BrowseNode> Expanded { get; init; }

    public required Lookup<BrowseNode>[] Places { get; init; }
}
