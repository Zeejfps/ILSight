namespace ILSight.Core;

/// <summary>
/// What speaks to the NuGet feeds and fetches the packages' icons, all through one handler: one
/// <see cref="NuGetFeed"/> per URL, so each service index is read once, and the icons a few at a time.
/// It is infrastructure: adapters hold it and pass it the values their ports carry.
/// </summary>
public sealed class NuGetClient
{
    private readonly Dictionary<FeedUrl, NuGetFeed> _feeds = [];
    private readonly HttpMessageHandler? _handler;

    /// <summary>A client whose requests <paramref name="handler"/> sends; the app's shared connections when null.</summary>
    public NuGetClient(HttpMessageHandler? handler = null)
    {
        _handler = handler;
        Icons = NuGetFeeds.CreateIcons(handler);
    }

    /// <summary>Where the packages' icons are fetched from.</summary>
    public PackageIcons Icons { get; }

    /// <summary>The feed at <paramref name="source"/>'s URL, made with that source the first time it is asked
    /// for. Call it from one thread only.</summary>
    public NuGetFeed EnsureFeed(NuGetSource source)
    {
        if (_feeds.TryGetValue(source.Url, out var feed))
        {
            return feed;
        }

        feed = NuGetFeeds.CreateFeed(source, _handler);
        _feeds.Add(source.Url, feed);
        return feed;
    }
}
