using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>A download <see cref="NuGetBrowser.StartDownload"/> started, for <see cref="NuGetBrowser.DownloadAsync"/> to
/// run: the package and version, the feed it was found in and the cache it goes to, and the work it runs as, which
/// closing the dialog cancels.</summary>
[MustUse]
public sealed class PackageDownloadTicket
{
    /// <summary>The package and version, as the dialog and a failure name them.</summary>
    public required string Name { get; init; }

    public required PackageCache Cache { get; init; }

    public required NuGetSource Feed { get; init; }

    public required PackageId Id { get; init; }

    public required PackageVersion Version { get; init; }

    public required WorkTicket Work { get; init; }
}
