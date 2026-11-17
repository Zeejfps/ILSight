using ILSight.Core;

namespace ILSight;

/// <summary>What a feed says of one version of a package: loading, read, or why it could not be.</summary>
public union VersionDetails(VersionDetails.Loading, VersionDetails.Read, VersionDetails.Failed)
{
    public sealed record Loading;

    public sealed record Read(PackageVersionDetails Details);

    public sealed record Failed(Exception Error);
}
