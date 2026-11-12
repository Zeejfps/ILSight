using ILSight.Core;

namespace ILSight.AssemblyHandling;

/// <summary>The nodes a reload found again: those the tree had expanded, with their children loaded, and
/// each tab's place, null where it is gone, in the order the places were asked for.</summary>
public sealed record ReloadedNodes
{
    public required List<BrowseNode> Expanded { get; init; }

    public required BrowseNode?[] Places { get; init; }
}
