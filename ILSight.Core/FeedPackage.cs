namespace ILSight.Core;

/// <summary>
/// A package a feed's search found: its latest version, what it says of itself, where its icon is,
/// and, newest first, every version listed with it. Text a feed leaves out is empty, counts it
/// leaves out are 0, and URLs it leaves out, or gives as no web URL, are null.
/// </summary>
public sealed record FeedPackage
{
    public required PackageId Id { get; init; }

    public required PackageVersion Version { get; init; }

    public required string Description { get; init; }

    public required string Authors { get; init; }

    public required WebUrl? IconUrl { get; init; }

    public required IReadOnlyList<FeedVersion> Versions { get; init; }

    /// <summary>How many times every version was downloaded.</summary>
    public long TotalDownloads { get; internal init; }

    /// <summary>Whether the feed vouches that the package's id prefix belongs to its owners.</summary>
    public bool Verified { get; internal init; }

    /// <summary>The accounts that own the package on the feed, joined by commas.</summary>
    public string Owners { get; internal init; } = "";

    public IReadOnlyList<string> Tags { get; internal init; } = [];

    public WebUrl? ProjectUrl { get; internal init; }

    public WebUrl? LicenseUrl { get; internal init; }

    /// <summary>How many vulnerabilities the feed knows of in the latest version.</summary>
    public int Vulnerabilities { get; internal init; }
}
