using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>Fetches the package a download the "open from NuGet" dialog started names
/// (<see cref="NuGetBrowser.StartDownload"/>) into the package cache, for the dialog to hand over, unless it dropped
/// the download by closing meanwhile; a failed download it still waits for is reported. Reaches the feed, the cache
/// and the user's error reports through <see cref="IPorts"/>.</summary>
public sealed class DownloadNuGetPackageHandler(DownloadNuGetPackageHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user a download failed.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The file of <paramref name="version"/> of package <paramref name="id"/>, fetched from
        /// <paramref name="feed"/> into <paramref name="cache"/> unless it is there; a download that failed
        /// comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<string>> DownloadPackageAsync(PackageCache cache, NuGetSource feed, PackageId id, PackageVersion version, CancellationToken ct);
    }

    public async Task HandleAsync(NuGetBrowser browser, PackageDownloadTicket download, CancellationToken ct)
    {
        var fetched = await ports.DownloadPackageAsync(download.Cache, download.Feed, download.Id, download.Version, ct);
        if (!browser.TryFinishDownload(download, fetched))
        {
            return;
        }

        if (fetched is Outcome<string>.Failed failed)
        {
            ports.ReportError(new AppProblem.PackageNotDownloaded(download.Name, failed.Error));
        }
    }
}
