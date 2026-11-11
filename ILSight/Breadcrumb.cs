using ILSight.Core;

namespace ILSight;

/// <summary>One step of a <see cref="Breadcrumb"/>: a node on the path and its name as the tree labels it.</summary>
public readonly record struct Crumb(BrowseNode Node, string Label);

/// <summary>The path from the root down to the node a tab shows, as the tree nests it. Built again
/// only when the node or the tree's shape changes.</summary>
public sealed class Breadcrumb
{
    /// <summary>The crumbs, by the node and the tree's revision they were built for.</summary>
    private Memo<(ByReference<BrowseNode>, int), IReadOnlyList<Crumb>> _path;

    public IReadOnlyList<Crumb> Crumbs => _path.Last ?? [];

    /// <summary>Follows <paramref name="node"/> as <paramref name="tree"/> shows it; none when it is null.
    /// Runs each frame, so listing the crumbs is a function of its own: a lambda here would allocate its
    /// closure on the way to returning what is already known.</summary>
    public void Follow(BrowseNode? node, BrowseTree tree)
    {
        if (_path.TryGet((new ByReference<BrowseNode>(node), tree.Revision), out _))
        {
            return;
        }

        _path = new Memo<(ByReference<BrowseNode>, int), IReadOnlyList<Crumb>>((new ByReference<BrowseNode>(node), tree.Revision), ListCrumbs(node, tree));
    }

    private static Crumb[] ListCrumbs(BrowseNode? node, BrowseTree tree)
    {
        if (node is null)
        {
            return [];
        }

        return Trees.ListPathTo(tree, node)
            .Select(n => new Crumb(n, GetCrumbLabel(n, tree)))
            .ToArray();
    }

    /// <summary>An assembly's name as its tab titles it, with the version that tells apart those of one
    /// name; any other node's as its row labels it.</summary>
    private static string GetCrumbLabel(BrowseNode node, BrowseTree tree)
    {
        if (node.Subject is NodeSubject.Module)
        {
            return Trees.GetLabel(tree, node);
        }

        return Trees.GetRowLabel(tree, node);
    }
}
