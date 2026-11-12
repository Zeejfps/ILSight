namespace ILSight.NuGetArea;

/// <summary>The feed last searched, as a settings file can hold it. Without a feed it is none.</summary>
internal sealed record NuGetSearchJson
{
    public string? FeedUrl { get; init; }

    public bool IncludePrerelease { get; init; }
}
