using ILSight.Core;

namespace ILSight;

/// <summary>Builds the children of a node a tree showed loading through <see cref="IPorts"/>, without holding up the
/// screen, then has the tree show them, or why they could not be built.</summary>
public sealed class BuildChildrenHandler(BuildChildrenHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Builds the children of <paramref name="node"/>, unless they were built already, without holding up
        /// the screen; children that could not be built keep why as their state (<see cref="ChildrenState.Failed"/>),
        /// which the tree shows. The cancellation of <paramref name="ct"/> is let through.</summary>
        Task BuildChildrenAsync(BrowseNode node, CancellationToken ct);
    }

    /// <summary>Builds the children of <paramref name="node"/>, which <paramref name="tree"/> asked for, and has it
    /// show what they came to: the children, or why they could not be built.</summary>
    public async Task HandleAsync(BrowseTree tree, BrowseNode node, CancellationToken ct)
    {
        await ports.BuildChildrenAsync(node, ct);
        tree.Builds.Finish(node);
        tree.View.Refresh();
    }
}
