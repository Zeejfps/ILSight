using ILSight.Core;
using ILSight.Core.Resources;

namespace ILSight.NuGetArea;

/// <summary>Fetches package icons through <paramref name="client"/>, which keeps the icons it speaks to, for
/// <see cref="FetchNuGetIconHandler"/>. What a fetch throws comes back as <see cref="Outcome{T}.Failed"/>, except the
/// cancellation of the token it was given, which it lets through; a timeout nobody asked for is a failure.</summary>
public sealed class FetchNuGetIconHandlerAdapter(NuGetClient client) : FetchNuGetIconHandler.IPorts
{
    /// <summary>Fetches and comes back on the UI thread, with whatever the fetch threw as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<ResourceImage>> DownloadIconAsync(WebUrl url, int side, CancellationToken ct)
    {
        var icon = await Background.Wait(NuGetFeeds.GetIconAsync(client.Icons, url, side, ct));
        ct.ThrowIfCancellationRequested();
        return icon;
    }
}
