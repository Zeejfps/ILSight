using System.Diagnostics;

namespace ILSight.Core;

/// <summary>A NuGet package source: the name the user gave it and the URL of its service index.</summary>
public sealed record NuGetSource
{
    private const string ServiceIndexName = "index.json";

    public static readonly NuGetSource NuGetOrg =
        new("nuget.org", WebUrl.Parse("https://api.nuget.org/v3/index.json") ?? throw new UnreachableException());

    private NuGetSource(string name, WebUrl url)
    {
        Name = name;
        Url = new FeedUrl { Address = url };
    }

    public string Name { get; }

    /// <summary>Its service index URL, which is the feed's identity: compared ignoring case.</summary>
    public FeedUrl Url { get; }

    /// <summary>The source a typed <paramref name="url"/> names, called after its host; null unless it
    /// is an absolute HTTP or HTTPS URL.</summary>
    public static NuGetSource? FromUrl(string url)
    {
        var web = WebUrl.Parse(url);
        if (web is null)
        {
            return null;
        }

        return new NuGetSource(web.Uri.Host, web);
    }

    /// <summary>The source a config adds as <paramref name="name"/>; null unless <paramref name="url"/>
    /// is the service index of a feed served over HTTP, which a v2 feed or a folder of packages is not.</summary>
    public static NuGetSource? FromConfig(string name, string url)
    {
        var web = WebUrl.Parse(url);
        if (web is null)
        {
            return null;
        }

        if (!web.Uri.AbsolutePath.EndsWith(ServiceIndexName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new NuGetSource(name, web);
    }
}
