using ILSight.Core;

namespace ILSight;

/// <summary>Expanding every assembly and the namespaces in it, as <see cref="BrowseTree.ExpandNamespaces"/> began it, for
/// <see cref="ExpandNamespacesHandler"/> to finish once each assembly's children are built: the assemblies, and the
/// expansion's place among the tree's, which collapsing it or a newer expansion makes stale. Dropped, the assemblies
/// are expanded with their namespaces folded.</summary>
[MustUse]
public sealed class NamespaceExpansion
{
    public required IReadOnlyList<BrowseNode> Roots { get; init; }

    public required int Ticket { get; init; }
}
