using ILSight.Core;
using ILSight.Core.Resources;

namespace ILSight.NuGetArea;

/// <summary>Fetches the icon of a package the "open from NuGet" dialog shows for the first time
/// (<see cref="NuGetBrowser.TakeIconsToFetch"/>): it gets its pixels unless it cannot be fetched, which leaves its
/// row the stand-in. Reaches the icon through <see cref="IPorts"/>.</summary>
public sealed class FetchNuGetIconHandler(FetchNuGetIconHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>The icon at <paramref name="url"/>, <paramref name="side"/> pixels across; one that cannot be
        /// fetched comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<ResourceImage>> DownloadIconAsync(WebUrl url, int side, CancellationToken ct);
    }

    /// <summary>How many pixels across a package's icon is fetched at, enough for its row on a screen
    /// of twice the usual density.</summary>
    public const int IconPixels = 64;

    internal async Task HandleAsync(PackageIconFetch fetch, CancellationToken ct)
    {
        var fetched = await ports.DownloadIconAsync(fetch.Url, IconPixels, ct);
        if (fetched is Outcome<ResourceImage>.Ok ok)
        {
            fetch.Icon.Show(ok.Value);
        }
    }
}
