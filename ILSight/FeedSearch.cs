using ILSight.Core;

namespace ILSight;

/// <summary>What a search asked of which feed.</summary>
public sealed record FeedSearch
{
    public required NuGetSource Feed { get; init; }

    public required string Query { get; init; }

    public required bool IncludePrerelease { get; init; }

    public required int Page { get; init; }
}
