namespace ILSight.Core;

/// <summary>The feeds the user's config lists, in its order, to search in or to pick from; one feed
/// per URL, so each service index is read once; where the packages' icons come from; and the cache
/// the packages picked are fetched into.</summary>
public sealed class NuGetSetup
{
    /// <summary>Sends the feeds' requests; the app's shared connections when null.</summary>
    public required HttpMessageHandler? Handler { get; init; }

    public readonly Dictionary<string, NuGetFeed> Feeds = new(StringComparer.OrdinalIgnoreCase);

    public required IReadOnlyList<NuGetSource> Sources { get; init; }

    public required PackageCache Cache { get; init; }

    public required PackageIcons Icons { get; init; }
}
