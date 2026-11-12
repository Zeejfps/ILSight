namespace ILSight.Core;

/// <summary>
/// What a feed's catalog says of one version of a package: when it was published, null when the feed
/// does not say or it is unlisted; its license, as an SPDX expression or else a URL; its project
/// page; the size of its file; what it depends on for each framework; and why it is deprecated, or
/// the vulnerabilities known in it, when either is so.
/// </summary>
public sealed record PackageVersionDetails(
    DateTimeOffset? Published,
    string License,
    WebUrl? LicenseUrl,
    WebUrl? ProjectUrl,
    long PackageSize,
    IReadOnlyList<DependencyGroup> DependencyGroups,
    PackageDeprecation? Deprecation,
    IReadOnlyList<PackageVulnerability> Vulnerabilities);
