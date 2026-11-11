namespace ILSight.Core;

/// <summary>The feeds the user's config lists, in its order, to search in or to pick from, and the cache
/// the packages picked are fetched into. The feeds are spoken to through a <see cref="NuGetClient"/>.</summary>
public sealed record NuGetSetup
{
    public required IReadOnlyList<NuGetSource> Sources { get; init; }

    public required PackageCache Cache { get; init; }
}
