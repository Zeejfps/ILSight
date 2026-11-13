namespace ILSight.Core;

/// <summary>The feeds the user's config lists, in its order, to search in or to pick from, and the cache
/// the packages picked are fetched into. The feeds are spoken to through a <see cref="NuGetClient"/>.</summary>
public sealed record NuGetSetup
{
    public required IReadOnlyList<NuGetSource> Sources { get; init; }

    public required PackageCache Cache { get; init; }

    /// <summary>Why the user's NuGet.Config couldn't be read, when nuget.org stands in for its sources; null when it
    /// was read, or there is none.</summary>
    public string? ConfigUnreadable { get; init; }
}
