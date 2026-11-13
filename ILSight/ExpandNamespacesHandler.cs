using ILSight.Core;

namespace ILSight;

/// <summary>Finishes expanding an assembly and the namespaces in it (<see cref="BrowseTree.ExpandNamespaces"/>): builds its
/// children through <see cref="IPorts"/>, without holding up the screen, then expands the namespaces among them, unless
/// the tree was collapsed or expanded again meanwhile. Children that could not be built are left as they are.</summary>
public sealed class ExpandNamespacesHandler(ExpandNamespacesHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Builds the children of the assembly <paramref name="assembly"/>, unless they were built already,
        /// without holding up the screen, and answers what they came to; a build that threw comes back as
        /// <see cref="Outcome{T}.Failed"/>. The cancellation of <paramref name="ct"/> is let through.</summary>
        Task<Outcome<ChildrenState>> BuildAssemblyChildrenAsync(BrowseNode assembly, CancellationToken ct);
    }

    /// <summary>Builds the children of <paramref name="root"/> and expands the namespaces among them in
    /// <paramref name="tree"/>, unless <paramref name="expansion"/> went stale meanwhile.</summary>
    public async Task HandleAsync(BrowseTree tree, NamespaceExpansion expansion, BrowseNode root, CancellationToken ct)
    {
        var built = await ports.BuildAssemblyChildrenAsync(root, ct);
        if (tree.Expansion.IsStale(expansion.Ticket))
        {
            return;
        }

        if (built is not Outcome<ChildrenState>.Ok ok)
        {
            return;
        }

        if (ok.Value is not ChildrenState.Ready loaded)
        {
            return;
        }

        tree.ExpandNamespacesIn(loaded.Children);
    }
}
