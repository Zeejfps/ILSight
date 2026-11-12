namespace ILSight.Core;

/// <summary>
/// What a feed's catalog says of one version of a package: when it was published, null when the feed
/// does not say or it is unlisted; its license, as an SPDX expression or else a URL; its project
/// page; the size of its file; what it depends on for each framework; and why it is deprecated, or
/// the vulnerabilities known in it, when either is so.
/// </summary>
public sealed record PackageVersionDetails
{
    public required DateTimeOffset? Published { get; init; }

    public required string License { get; init; }

    public required WebUrl? LicenseUrl { get; init; }

    public required WebUrl? ProjectUrl { get; init; }

    public required long PackageSize { get; init; }

    public required IReadOnlyList<DependencyGroup> DependencyGroups { get; init; }

    public required PackageDeprecation? Deprecation { get; init; }

    public required IReadOnlyList<PackageVulnerability> Vulnerabilities { get; init; }
}
