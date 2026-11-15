namespace ILSight.Core;

/// <summary>What finding an entity's node in a browse tree came to: its node; none, as the tree lists no node for it
/// (an accessor, a compiler-generated member); or the children that would list it failed to build, and why.</summary>
internal abstract record NodePlace
{
    private NodePlace()
    {
    }

    public sealed record Listed(BrowseNode Node) : NodePlace;

    public sealed record NotListed : NodePlace;

    /// <summary>The one answer for an entity the tree lists no node for.</summary>
    public static readonly NotListed None = new();

    public sealed record Failed(Exception Error) : NodePlace;

    /// <summary>The node found, or null for an entity not listed or one whose place failed.</summary>
    public BrowseNode? NodeOrNull => this is Listed listed ? listed.Node : null;
}
