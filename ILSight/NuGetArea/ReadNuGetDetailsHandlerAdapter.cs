using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>Reads what the NuGet feeds' catalogs say of package versions through <paramref name="client"/>, which
/// keeps the feeds it speaks to, for <see cref="ReadNuGetDetailsHandler"/>. What a read throws comes back as
/// <see cref="Outcome{T}.Failed"/>, except the cancellation of the token it was given, which it lets through; a
/// timeout nobody asked for is a failure.</summary>
public sealed class ReadNuGetDetailsHandlerAdapter(NuGetClient client) : ReadNuGetDetailsHandler.IPorts
{
    /// <summary>Reads and comes back on the UI thread, with a request that failed or timed out (<see cref="NetworkFailures"/>) as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public Task<Outcome<PackageVersionDetails>> ReadDetailsAsync(NuGetSource feed, PackageId id, FeedVersion version, CancellationToken ct)
    {
        return NetworkFailures.CatchAsync(Background.Wait(NuGetFeeds.ReadDetailsAsync(client.EnsureFeed(feed), id, version, ct)), ct);
    }
}
