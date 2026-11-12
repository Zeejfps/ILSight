namespace ILSight.Core;

/// <summary>A search hit: the <see cref="Node"/> found, its label, and where it lives.</summary>
public sealed record SearchResult
{
    public required BrowseNode Node { get; init; }

    public required string Text { get; init; }

    /// <summary>Where it lives, as ILSpy's results show it: the namespace and enclosing type of a type or member,
    /// <c>-</c> for the global namespace, the resources file holding a resource entry, an assembly's file; empty for a
    /// namespace or a resource of its own.</summary>
    public required string Location { get; init; }

    /// <summary>How closely the hit matches, as ILSpy ranks it: the shorter the name the closer, and 0 for
    /// compiler-generated names.</summary>
    public required float Fitness { get; init; }
}
