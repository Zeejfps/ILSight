using System.Diagnostics;

namespace ILSight.Core;

/// <summary>What finding an entity's node in a browse tree came to: its node; none, as the tree lists no node for it
/// (an accessor, a compiler-generated member); the children that would list it failed to build, and why; or the library
/// failed reading the module's metadata on the way, the module's failure, with why.</summary>
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

    /// <summary>The library failed reading the module's metadata on the way to the place: the user's data, said as the
    /// module's (unreadable), never the app's failure.</summary>
    public sealed record Unreadable(UnreadableModule Module) : NodePlace;

    /// <summary>The node found, or null for an entity not listed or one whose place failed.</summary>
    public BrowseNode? NodeOrNull => this is Listed listed ? listed.Node : null;

    /// <summary>What a lookup of one target answers for this place: its node, not found for an entity the tree doesn't
    /// list, the children's failure, with why, for one whose place failed: never not found for that; and the module
    /// unreadable when the library failed reading it on the way.</summary>
    public Outcome<Lookup<BrowseNode>> ToLookup()
    {
        return this switch
        {
            Listed listed => new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Found { Value = listed.Node }),
            NotListed => new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound()),
            Failed failed => new Outcome<Lookup<BrowseNode>>.Failed(failed.Error),
            Unreadable unreadable => new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Unreadable { Module = unreadable.Module }),
            _ => throw new UnreachableException($"NodePlace.ToLookup does not handle {GetType().Name}."),
        };
    }
}
