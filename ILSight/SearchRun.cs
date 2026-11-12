using ILSight.Core;

namespace ILSight;

/// <summary>A run of a search <see cref="Searches.Update"/> asked for, for <see cref="RunSearchHandler"/> to run: the
/// query, the assemblies it searches, and the work it runs as, which a new query cancels. Dropped, the search says it
/// is busy for good.</summary>
[MustUse]
public sealed class SearchRun
{
    public required SearchQuery Query { get; init; }

    public required IReadOnlyList<LoadedModule> Within { get; init; }

    public CancellationTokenSource Work { get; } = new();
}
