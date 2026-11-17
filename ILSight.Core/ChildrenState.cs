namespace ILSight.Core;

/// <summary>Where the children of a node stand: not asked for yet, being built, built, or failed to build.</summary>
public union ChildrenState(ChildrenState.Unloaded, ChildrenState.Running, ChildrenState.Ready, ChildrenState.Failed)
{
    public sealed record Unloaded;

    /// <summary>The one state of children not asked for yet.</summary>
    public static readonly Unloaded NotLoaded = new();

    /// <summary>Being built; <see cref="Load"/> ends <see cref="Ready"/> or <see cref="Failed"/>.</summary>
    public sealed record Running(Task<ChildrenState> Load);

    public sealed record Ready(IReadOnlyList<BrowseNode> Children);

    public sealed record Failed(Exception Error);
}
