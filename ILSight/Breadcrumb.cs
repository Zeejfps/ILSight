using ILSight.Core;

namespace ILSight;

/// <summary>One step of a <see cref="Breadcrumb"/>: a node on the path and its name as the tree labels it.</summary>
public readonly record struct Crumb(BrowseNode Node, string Label);

/// <summary>The path from the root down to the node a tab shows, as the tree nests it. Built again
/// only when the node or the tree's shape changes.</summary>
public sealed class Breadcrumb
{
    /// <summary>The crumbs, by the node and the tree's revision they were built for.</summary>
    public Memo<(ByReference<BrowseNode>, int), IReadOnlyList<Crumb>> Path;

    public IReadOnlyList<Crumb> Crumbs => Path.Last ?? [];
}
