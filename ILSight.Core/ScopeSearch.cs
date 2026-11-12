namespace ILSight.Core;

/// <summary>What a search over the modules of a scope came to: what it found in those the library could read, and
/// each module it reached that the library could not read, with why, named beside them rather than ending the
/// search.</summary>
public sealed record ScopeSearch<T>
{
    public required IReadOnlyList<T> Found { get; init; }

    /// <summary>The modules the search reached that the library could not read, by file name.</summary>
    public required IReadOnlyList<UnreadableModule> Unreadable { get; init; }
}
