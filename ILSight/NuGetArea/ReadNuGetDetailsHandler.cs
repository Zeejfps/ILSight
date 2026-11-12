using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>Reads what a feed's catalog says of a package version the "open from NuGet" dialog shows for the first
/// time (<see cref="NuGetBrowser.TakeDetailsToFetch"/>), which takes the place of its loading unless it was dropped or
/// asked for again meanwhile. Reaches the feed through <see cref="IPorts"/>.</summary>
public sealed class ReadNuGetDetailsHandler(ReadNuGetDetailsHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>What <paramref name="feed"/>'s catalog says of <paramref name="version"/> of package
        /// <paramref name="id"/>; a read that failed comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<PackageVersionDetails>> ReadDetailsAsync(NuGetSource feed, PackageId id, FeedVersion version, CancellationToken ct);
    }

    internal async Task HandleAsync(NuGetBrowser browser, VersionDetailsFetch fetch, CancellationToken ct)
    {
        var read = await ports.ReadDetailsAsync(fetch.Feed, fetch.Key.Id, fetch.Version, ct);
        browser.ShowDetails(fetch, read);
    }
}
