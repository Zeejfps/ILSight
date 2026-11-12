using ILSight.Core;

namespace ILSight;

/// <summary>What a feed says of one version of a package: loading, read, or why it could not be.</summary>
public abstract record VersionDetails
{
    private VersionDetails()
    {
    }

    public sealed record Loading : VersionDetails;

    public sealed record Read(PackageVersionDetails Details) : VersionDetails;

    public sealed record Failed(string Error) : VersionDetails;
}
