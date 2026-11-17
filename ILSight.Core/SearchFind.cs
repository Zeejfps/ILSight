namespace ILSight.Core;

/// <summary>What a search comes to in the assemblies it searches: a hit, or a part it couldn't read that could have
/// held one, and why: an assembly it couldn't search on, one it searched only in part, or a match it found and couldn't
/// show.</summary>
public union SearchFind(SearchFind.Hit, SearchFind.Unsearched, SearchFind.PartlySearched, SearchFind.NotShown)
{
    public sealed record Hit
    {
        public required SearchResult Result { get; init; }
    }

    /// <summary>The library failed on <see cref="Assembly"/> while it was searched: the hits found in it before stand,
    /// and the rest of it is not searched.</summary>
    public sealed record Unsearched
    {
        public required LoadedModule Assembly { get; init; }

        public required string Why { get; init; }

        /// <summary>What the library threw, so its record has the diagnostic (CF3).</summary>
        public required Exception Error { get; init; }
    }

    /// <summary><see cref="Assembly"/> was searched but for <see cref="Count"/> method bodies whose IL the library
    /// couldn't read, the first for <see cref="Why"/>: what they hold is not searched.</summary>
    public sealed record PartlySearched
    {
        public required LoadedModule Assembly { get; init; }

        public required int Count { get; init; }

        public required string Why { get; init; }

        /// <summary>What the library threw on the first body, so its record has the diagnostic (CF3).</summary>
        public required Exception Error { get; init; }
    }

    /// <summary>A match in <see cref="Assembly"/> whose place in the tree the library couldn't read, so no node shows
    /// it: named by <see cref="Name"/> where the search read it, else by <see cref="Token"/>.</summary>
    public sealed record NotShown
    {
        public required LoadedModule Assembly { get; init; }

        public required int Token { get; init; }

        public required string? Name { get; init; }

        public required string Why { get; init; }

        /// <summary>What the library threw placing it, so its record has the diagnostic (CF3).</summary>
        public required Exception Error { get; init; }
    }
}
