using ILSight.Core;

namespace ILSight;

/// <summary>An icon asked for, still to be fetched, and where from.</summary>
internal sealed record PackageIconFetch
{
    public required WebUrl Url { get; init; }

    public required PackageIcon Icon { get; init; }
}
