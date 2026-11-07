using ILSight.Core.Resources;
using System.Net;
using System.Text.Json;

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
    public AsyncGuarded<ServiceIndex?> Index { get; } = new(null);

    public required NuGetSource Source { get; init; }

    /// <summary>The resources of the feed's service index this reads; a feed may describe no versions.</summary>
    public sealed record ServiceIndex(WebUrl Search, WebUrl PackageFolder, WebUrl? Registrations);
}

/// <summary>Speaking to NuGet feeds, and the packages and icons fetched from them.</summary>
public static class NuGetFeeds
{
    private const string SearchType = "SearchQueryService";
    private const string PackageFolderType = "PackageBaseAddress/3.0.0";
    private const string RegistrationsType = "RegistrationsBaseUrl";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The icons fetched through <paramref name="handler"/>; the app's shared connections by default.</summary>
    public static PackageIcons CreateIcons(HttpMessageHandler? handler = null)
    {
        return new PackageIcons { Client = GetClient(handler) };
    }

    /// <summary>The feed at <paramref name="source"/>, whose requests <paramref name="handler"/> sends; the app's
    /// shared connections by default.</summary>
    public static NuGetFeed CreateFeed(NuGetSource source, HttpMessageHandler? handler = null)
    {
        return new NuGetFeed { Source = source, Client = GetClient(handler) };
    }

    /// <summary>A client sending through <paramref name="handler"/>, or the app's shared one.</summary>
    internal static HttpClient GetClient(HttpMessageHandler? handler)
    {
        if (handler == null)
            return NuGetFeed.Shared.Value;
        return CreateClient(handler);
    }

    internal static HttpClient CreateClient(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { Timeout = Timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ILSight");
        return client;
    }

    /// <summary>How many pages a search that matched <paramref name="totalHits"/> packages has, up to
    /// the last one <see cref="NuGetFeed.MaxSkip"/> lets start; at least one.</summary>
    public static int CountPages(int totalHits)
    {
        var reachable = Math.Min(totalHits, NuGetFeed.MaxSkip + NuGetFeed.PageSize);
        var pages = (reachable + NuGetFeed.PageSize - 1) / NuGetFeed.PageSize;
        return Math.Max(1, pages);
    }

    /// <summary>Page <paramref name="page"/>, from 0, of the packages matching <paramref name="query"/>;
    /// of every package when the query is empty.</summary>
    /// <exception cref="HttpRequestException">The feed could not be reached or refused the request.</exception>
    /// <exception cref="InvalidDataException">The feed answered with something that is no search result.</exception>
    public static async Task<FeedPage> SearchAsync(NuGetFeed feed, string query, bool includePrerelease, int page = 0, CancellationToken cancellationToken = default)
    {
        var index = await EnsureIndexAsync(feed, cancellationToken).ConfigureAwait(false);
        var url = ParseDerivedUrl(GetSearchUrl(index.Search, query, includePrerelease, page));
        using var document = await ReadJsonAsync(feed, url, cancellationToken).ConfigureAwait(false);
        return FeedJson.ReadPage(document.RootElement);
    }

    private static string GetSearchUrl(WebUrl search, string query, bool includePrerelease, int page)
    {
        var q = Uri.EscapeDataString(query.Trim());
        var prerelease = includePrerelease ? "true" : "false";
        var skip = Math.Min(page * NuGetFeed.PageSize, NuGetFeed.MaxSkip);
        return $"{search}?q={q}&skip={skip}&take={NuGetFeed.PageSize}&prerelease={prerelease}&semVerLevel=2.0.0";
    }

    /// <summary>What the feed's catalog says of <paramref name="version"/> of the package <paramref name="id"/>:
    /// read from where the search said the feed describes it, else from the feed's registrations.</summary>
    /// <exception cref="HttpRequestException">The feed could not be reached, or has no such version.</exception>
    /// <exception cref="InvalidDataException">The feed describes no versions, or answered with something else.</exception>
    public static async Task<PackageVersionDetails> ReadDetailsAsync(NuGetFeed feed, PackageId id, FeedVersion version, CancellationToken cancellationToken = default)
    {
        var url = await FindDetailsUrlAsync(feed, id, version, cancellationToken).ConfigureAwait(false);
        using var leaf = await ReadJsonAsync(feed, url, cancellationToken).ConfigureAwait(false);
        var registration = FeedJson.ReadRegistration(leaf.RootElement);
        if (registration.CatalogEntry == null)
            throw new InvalidDataException($"{url} names no catalog entry.");
        return await ReadCatalogEntryAsync(feed, registration.CatalogEntry.Value, registration.Published, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<WebUrl> FindDetailsUrlAsync(NuGetFeed feed, PackageId id, FeedVersion version, CancellationToken cancellationToken)
    {
        if (version.DetailsUrl != null)
            return version.DetailsUrl;
        var index = await EnsureIndexAsync(feed, cancellationToken).ConfigureAwait(false);
        if (index.Registrations == null)
            throw new InvalidDataException($"The service index of {feed.Source.Url} names no {RegistrationsType}.");
        return ParseDerivedUrl($"{index.Registrations.Text.TrimEnd('/')}/{id.Lower}/{version.Version.Filed}.json");
    }

    /// <summary>The details in a catalog entry, which a registration leaf gives inline or as the URL to read it from.</summary>
    private static async Task<PackageVersionDetails> ReadCatalogEntryAsync(NuGetFeed feed, JsonElement entry, DateTimeOffset? listedAt, CancellationToken cancellationToken)
    {
        if (entry.ValueKind == JsonValueKind.Object)
            return FeedJson.ReadDetails(entry, listedAt);
        var url = entry.ValueKind == JsonValueKind.String ? WebUrl.Parse(entry.GetString()) : null;
        if (url == null)
            throw new InvalidDataException("A catalog entry is neither inline nor a web URL.");
        using var catalog = await ReadJsonAsync(feed, url, cancellationToken).ConfigureAwait(false);
        return FeedJson.ReadDetails(catalog.RootElement, listedAt);
    }

    /// <summary>Writes the package file of <paramref name="id"/> at <paramref name="version"/> to
    /// <paramref name="path"/>, which is replaced only once the whole file is in.</summary>
    /// <exception cref="HttpRequestException">The feed could not be reached, or has no such package.</exception>
    /// <exception cref="IOException">The file could not be written.</exception>
    public static async Task DownloadAsync(NuGetFeed feed, PackageId id, PackageVersion version, string path, CancellationToken cancellationToken = default)
    {
        var index = await EnsureIndexAsync(feed, cancellationToken).ConfigureAwait(false);
        var url = GetPackageUrl(index.PackageFolder, id, version);
        using var response = await feed.Client.GetAsync(url.Uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await WriteWholeFileAsync(response, path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Where the feed serves a package file: <c>{folder}/{id}/{version}/{id}.{version}.nupkg</c>,
    /// in lower case, the version without its build metadata.</summary>
    private static WebUrl GetPackageUrl(WebUrl folder, PackageId id, PackageVersion version)
    {
        var lowerId = id.Lower;
        var lowerVersion = version.Filed;
        return ParseDerivedUrl($"{folder.Text.TrimEnd('/')}/{lowerId}/{lowerVersion}/{lowerId}.{lowerVersion}.nupkg");
    }

    /// <summary>The URL <paramref name="text"/>, made from one the feed gave.</summary>
    /// <exception cref="InvalidDataException">What the feed gave makes no web URL.</exception>
    private static WebUrl ParseDerivedUrl(string text)
    {
        return WebUrl.Parse(text) ?? throw new InvalidDataException($"{text} is no web URL.");
    }

    private static async Task WriteWholeFileAsync(HttpResponseMessage response, string path, CancellationToken cancellationToken)
    {
        var temporary = path + ".download";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            await using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static async Task<NuGetFeed.ServiceIndex> EnsureIndexAsync(NuGetFeed feed, CancellationToken cancellationToken)
    {
        using var held = await feed.Index.EnterAsync(cancellationToken).ConfigureAwait(false);
        var index = held.Value;
        if (index != null)
            return index;
        index = await FetchIndexAsync(feed, cancellationToken).ConfigureAwait(false);
        held.Value = index;
        return index;
    }

    private static async Task<NuGetFeed.ServiceIndex> FetchIndexAsync(NuGetFeed feed, CancellationToken cancellationToken)
    {
        using var document = await ReadJsonAsync(feed, feed.Source.Url, cancellationToken).ConfigureAwait(false);
        var resources = FeedJson.ReadResources(document.RootElement);
        var search = FindResource(feed, resources, SearchType);
        var folder = FindResource(feed, resources, PackageFolderType);
        var registrations = FindOptionalResource(resources, RegistrationsType);
        return new NuGetFeed.ServiceIndex(search, folder, registrations);
    }

    /// <summary>The URL of the first resource whose type is <paramref name="type"/>, or a version of it.</summary>
    private static WebUrl FindResource(NuGetFeed feed, List<FeedResource> resources, string type)
    {
        var url = FindOptionalResource(resources, type);
        if (url == null)
            throw new InvalidDataException($"The service index of {feed.Source.Url} names no {type}.");
        return url;
    }

    /// <summary>The web URL of the first resource whose type is <paramref name="type"/>; null when there is
    /// none, or its URL is none.</summary>
    private static WebUrl? FindOptionalResource(List<FeedResource> resources, string type)
    {
        foreach (var resource in resources)
        {
            if (resource.Types.Any(t => IsOfType(t, type)))
                return resource.Url;
        }
        return null;
    }

    private static bool IsOfType(string found, string type)
    {
        if (found == type)
            return true;
        return found.StartsWith(type + "/", StringComparison.Ordinal);
    }

    private static async Task<JsonDocument> ReadJsonAsync(NuGetFeed feed, WebUrl url, CancellationToken cancellationToken)
    {
        using var response = await feed.Client.GetAsync(url.Uri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{url} answered with something that is not JSON: {e.Message}", e);
        }
    }

    /// <summary>The package file of <paramref name="id"/> at <paramref name="version"/> already on disk,
    /// or null.</summary>
    public static string? FindPackage(PackageCache cache, PackageId id, PackageVersion version)
    {
        var restored = GetPackagePath(cache.GlobalPackagesFolder, id, version);
        if (File.Exists(restored))
            return restored;
        var downloaded = GetPackagePath(cache.DownloadFolder, id, version);
        if (File.Exists(downloaded))
            return downloaded;
        return null;
    }

    /// <summary>The package file of <paramref name="id"/> at <paramref name="version"/>: the one on
    /// disk, else one downloaded from <paramref name="feed"/> now.</summary>
    /// <exception cref="HttpRequestException">The feed could not be reached, or has no such package.</exception>
    /// <exception cref="IOException">The package could not be written.</exception>
    public static async Task<string> FetchPackageAsync(PackageCache cache, NuGetFeed feed, PackageId id, PackageVersion version, CancellationToken cancellationToken = default)
    {
        var found = FindPackage(cache, id, version);
        if (found != null)
            return found;
        var path = GetPackagePath(cache.DownloadFolder, id, version);
        await NuGetFeeds.DownloadAsync(feed, id, version, path, cancellationToken).ConfigureAwait(false);
        return path;
    }

    /// <summary>Where a package sits in a folder laid out as the global packages folder is:
    /// <c>{id}/{version}/{id}.{version}.nupkg</c>, in lower case.</summary>
    private static string GetPackagePath(string folder, PackageId id, PackageVersion version)
    {
        var lowerId = id.Lower;
        var lowerVersion = version.Filed;
        return Path.Combine(folder, lowerId, lowerVersion, $"{lowerId}.{lowerVersion}.nupkg");
    }

    /// <summary>The largest icon file read; nuget.org takes none larger.</summary>
    private const int MaxBytes = 1024 * 1024;

    /// <summary>The picture at <paramref name="url"/>, shrunk to fit a square <paramref name="side"/>
    /// pixels across.</summary>
    /// <exception cref="HttpRequestException">The picture could not be fetched.</exception>
    /// <exception cref="InvalidDataException">It is no picture, or too large a file.</exception>
    public static async Task<ResourceImage> FetchIconAsync(PackageIcons icons, WebUrl url, int side, CancellationToken cancellationToken = default)
    {
        await icons.Fetches.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bytes = await ReadIconBytesAsync(icons, url, cancellationToken).ConfigureAwait(false);
            return ResourceImages.FitInto(ResourceImages.DecodeImage(bytes), side);
        }
        finally
        {
            icons.Fetches.Release();
        }
    }

    private static async Task<byte[]> ReadIconBytesAsync(PackageIcons icons, WebUrl url, CancellationToken cancellationToken)
    {
        using var response = await icons.Client.GetAsync(url.Uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxBytes)
            throw new InvalidDataException($"The icon at {url} is larger than {MaxBytes} bytes.");
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length > MaxBytes)
            throw new InvalidDataException($"The icon at {url} is larger than {MaxBytes} bytes.");
        return bytes;
    }

    /// <summary>The setup of the feeds of <paramref name="sources"/>, fetching into <paramref name="cache"/>, whose
    /// requests <paramref name="handler"/> sends; the app's shared connections by default.</summary>
    public static NuGetSetup CreateSetup(IReadOnlyList<NuGetSource> sources, PackageCache cache, HttpMessageHandler? handler = null)
    {
        return new NuGetSetup { Sources = sources, Cache = cache, Icons = NuGetFeeds.CreateIcons(handler), Handler = handler };
    }

    /// <summary>The setup the user's NuGet.Config describes, downloading into the app's own folder.</summary>
    public static NuGetSetup ReadUserSetup()
    {
        return CreateSetupFromConfig(NuGetConfigs.ReadUser());
    }

    /// <summary>The setup when the user's config cannot be read at all: nuget.org and the default folders.</summary>
    public static NuGetSetup CreateDefaultSetup()
    {
        return CreateSetupFromConfig(NuGetConfigs.CreateDefault());
    }

    private static NuGetSetup CreateSetupFromConfig(NuGetConfig config)
    {
        var cache = new PackageCache { GlobalPackagesFolder = config.GlobalPackagesFolder, DownloadFolder = PackageCache.UserDownloadFolder };
        return NuGetFeeds.CreateSetup(config.Sources, cache);
    }

    /// <summary>The feed at <paramref name="source"/>'s URL, made the first time it is asked for, named
    /// as the config names it when the config lists it. Call it from one thread only.</summary>
    public static NuGetFeed EnsureFeed(NuGetSetup setup, NuGetSource source)
    {
        if (setup.Feeds.TryGetValue(source.Url.Text, out var feed))
            return feed;
        var named = GetListedSource(setup, source);
        feed = NuGetFeeds.CreateFeed(named, setup.Handler);
        setup.Feeds.Add(source.Url.Text, feed);
        return feed;
    }

    private static NuGetSource GetListedSource(NuGetSetup setup, NuGetSource source)
    {
        var listed = setup.Sources.FirstOrDefault(s => string.Equals(s.Url.Text, source.Url.Text, StringComparison.OrdinalIgnoreCase));
        if (listed == null)
            return source;
        return listed;
    }
}
