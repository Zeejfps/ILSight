using System.Net;

namespace ILSight.Core;

/// <summary>
/// A NuGet feed spoken to over the v3 protocol: its service index, read the first time it is needed,
/// names the search service, the folder of package files the feed serves, and where it describes
/// each version. Every call may be made from any thread.
/// </summary>
public sealed class NuGetFeed
{
    /// <summary>How many packages a page of a search holds.</summary>
    public const int PageSize = 25;

    /// <summary>The furthest into a search nuget.org lets a page start; every feed is held to it.</summary>
    public const int MaxSkip = 3000;

    /// <summary>The one client the feeds share, made once whichever thread asks first.</summary>
    internal static readonly Lazy<HttpClient> Shared = new(static () => NuGetFeeds.CreateClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All }));

    public required HttpClient Client { get; init; }

    /// <summary>The service index, once it is read, which one call at a time reads; a failed read leaves it
    /// unread for the next. <see cref="NuGetFeeds.EnsureIndexAsync"/> hands it back.</summary>
    internal AsyncGuarded<ServiceIndex?> Index { get; } = new(null);

    public required NuGetSource Source { get; init; }
}
