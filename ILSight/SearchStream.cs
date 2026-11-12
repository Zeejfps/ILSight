using ILSight.Core;

namespace ILSight;

/// <summary>Streams the hits of <paramref name="query"/> in <paramref name="within"/>, of
/// <paramref name="assemblies"/>, in batches.</summary>
public delegate IAsyncEnumerable<IReadOnlyList<SearchResult>> SearchStream(AssemblyList assemblies, SearchQuery query,
    IEnumerable<LoadedModule> within, CancellationToken cancellationToken);
