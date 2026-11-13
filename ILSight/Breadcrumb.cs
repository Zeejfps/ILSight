using ILSight.Core;

namespace ILSight;

/// <summary>The path from the root down to the node a tab shows, as the tree nests it. Built again
/// only when the node or the tree's shape changes.</summary>
public sealed class Breadcrumb
{
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

        return tree.ListPathTo(node)
            .Select(n => new Crumb { Node = n, Label = GetCrumbLabel(n, tree) })
            .ToArray();
    }

    private static string GetCrumbLabel(BrowseNode node, BrowseTree tree)
    {
        // An assembly's tab title carries the version that tells apart those of one name.
        if (node.Subject is NodeSubject.Module)
        {
            return tree.GetLabel(node);
        }

        return tree.GetRowLabel(node);
    }
}
