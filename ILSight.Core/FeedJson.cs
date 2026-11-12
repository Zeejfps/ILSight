using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ILSight.Core;

/// <summary>
/// Reads what a NuGet feed answers in JSON: a page of its search, a version's registration and catalog
/// entry, and the service index. What a feed leaves out reads as empty, 0 or null. A package or resource
/// of another shape is left out on its own; an answer of another shape cannot be read.
/// </summary>
internal static partial class FeedJson
{
    /// <summary>The year a feed dates a version it unlisted.</summary>
    private const int UnlistedYear = 1900;

    /// <summary>The page of packages <paramref name="root"/> holds, without those that cannot be read.</summary>
    /// <exception cref="InvalidDataException">It is no page of a search.</exception>
    public static FeedPage ReadPage(JsonElement root)
    {
        var page = Read(root, FeedJsonContext.Default.SearchPageJson, "a page of a search");
        if (page.Data is null)
        {
            throw new InvalidDataException("A page of a search holds no list named data.");
        }

        var packages = page.Data
            .Select(static p => TryRead(p, FeedJsonContext.Default.PackageJson))
            .OfType<PackageJson>()
            .Select(static p => Parse(p))
            .OfType<FeedPackage>()
            .ToList();
        var total = (int)Math.Min(page.TotalHits ?? 0, int.MaxValue);
        return new FeedPage { Packages = packages, TotalHits = Math.Max(total, packages.Count) };
    }

    /// <exception cref="InvalidDataException">It is no registration of a version.</exception>
    public static FeedRegistration ReadRegistration(JsonElement root)
    {
        var registration = Read(root, FeedJsonContext.Default.RegistrationJson, "a registration");
        return new FeedRegistration
        {
            Published = ReadDate(registration.Published),
            CatalogEntry = registration.CatalogEntry,
        };
    }

    /// <summary>The details in a version's catalog entry, published when <paramref name="listedAt"/>
    /// says unless the entry itself says.</summary>
    /// <exception cref="InvalidDataException">It is no catalog entry.</exception>
    public static PackageVersionDetails ReadDetails(JsonElement entry, DateTimeOffset? listedAt)
    {
        return Parse(Read(entry, FeedJsonContext.Default.CatalogEntryJson, "a catalog entry"), listedAt);
    }

    /// <summary>The resources the service index <paramref name="root"/> lists, without those that cannot be read.</summary>
    /// <exception cref="InvalidDataException">It is no service index.</exception>
    public static List<FeedResource> ReadResources(JsonElement root)
    {
        var index = Read(root, FeedJsonContext.Default.ServiceIndexJson, "a service index");
        if (index.Resources is null)
        {
            throw new InvalidDataException("A service index holds no list named resources.");
        }

        return index.Resources
            .Select(static r => TryRead(r, FeedJsonContext.Default.ResourceJson))
            .OfType<ResourceJson>()
            .Select(static r => Parse(r))
            .ToList();
    }

    /// <summary>When a version was published; null when the feed does not say, or dates it as unlisted.</summary>
    public static DateTimeOffset? ReadDate(string? text)
    {
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
        {
            return null;
        }

        if (date.Year <= UnlistedYear)
        {
            return null;
        }

        return date;
    }

    private static T Read<T>(JsonElement element, JsonTypeInfo<T> type, string what) where T : class
    {
        try
        {
            return element.Deserialize(type) ?? throw new InvalidDataException($"Expected {what}, found null.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"Expected {what}: {e.Message}", e);
        }
    }

    private static T? TryRead<T>(JsonElement element, JsonTypeInfo<T> type) where T : class
    {
        try
        {
            return element.Deserialize(type);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The package; null when its id or version is none NuGet allows.</summary>
    private static FeedPackage? Parse(PackageJson json)
    {
        var id = PackageId.Parse(json.Id ?? "");
        var version = PackageVersion.Parse(json.Version ?? "");
        if (id is null || version is null)
        {
            return null;
        }

        return new FeedPackage
        {
            Id = id,
            Version = version,
            Description = json.Description ?? "",
            Authors = JoinNames(json.Authors),
            IconUrl = WebUrl.Parse(json.IconUrl),
            Versions = ListVersions(json.Versions, version),
            TotalDownloads = json.TotalDownloads ?? 0,
            Verified = json.Verified ?? false,
            Owners = JoinNames(json.Owners),
            Tags = SplitWords(json.Tags),
            ProjectUrl = WebUrl.Parse(json.ProjectUrl),
            LicenseUrl = WebUrl.Parse(json.LicenseUrl),
            Vulnerabilities = json.Vulnerabilities?.Count ?? 0,
        };
    }

    /// <summary>The versions listed, newest first, without those NuGet allows no such version of; just
    /// <paramref name="latest"/> when the feed lists none.</summary>
    private static List<FeedVersion> ListVersions(List<VersionJson?>? listed, PackageVersion latest)
    {
        var versions = (listed ?? [])
            .OfType<VersionJson>()
            .Select(static v => Parse(v))
            .OfType<FeedVersion>()
            .Reverse()
            .ToList();
        if (versions.Count == 0)
        {
            versions.Add(new FeedVersion { Version = latest, Downloads = 0, DetailsUrl = null });
        }

        return versions;
    }

    /// <summary>Names a feed gives as one string or as a list of them, joined by commas.</summary>
    private static string JoinNames(TextListJson? names)
    {
        return names is null ? "" : string.Join(", ", names.Items.Where(static n => n.Length > 0));
    }

    /// <summary>Words a feed gives as a list of them or as one string, split on spaces.</summary>
    private static List<string> SplitWords(TextListJson? words)
    {
        return words is null ? [] : words.Items.SelectMany(static w => w.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList();
    }

    private static FeedVersion? Parse(VersionJson json)
    {
        var version = PackageVersion.Parse(json.Version ?? "");
        if (version is null)
        {
            return null;
        }

        return new FeedVersion
        {
            Version = version,
            Downloads = json.Downloads ?? 0,
            DetailsUrl = WebUrl.Parse(json.Id),
        };
    }

    private static PackageVersionDetails Parse(CatalogEntryJson json, DateTimeOffset? listedAt)
    {
        return new PackageVersionDetails
        {
            Published = ReadDate(json.Published) ?? listedAt,
            License = json.LicenseExpression ?? "",
            LicenseUrl = WebUrl.Parse(json.LicenseUrl),
            ProjectUrl = WebUrl.Parse(json.ProjectUrl),
            PackageSize = json.PackageSize ?? 0,
            DependencyGroups = (json.DependencyGroups ?? []).OfType<DependencyGroupJson>().Select(static g => Parse(g)).ToList(),
            Deprecation = json.Deprecation is null ? null : Parse(json.Deprecation),
            Vulnerabilities = (json.Vulnerabilities ?? []).OfType<VulnerabilityJson>().Select(static v => Parse(v)).ToList(),
        };
    }

    private static DependencyGroup Parse(DependencyGroupJson json)
    {
        return new DependencyGroup
        {
            TargetFramework = json.TargetFramework ?? "",
            Dependencies = (json.Dependencies ?? []).OfType<DependencyJson>().Select(static d => new PackageDependency
            {
                Id = d.Id ?? "",
                Range = d.Range ?? "",
            }).ToList(),
        };
    }

    private static PackageDeprecation Parse(DeprecationJson json)
    {
        return new PackageDeprecation
        {
            Reasons = json.Reasons is null ? [] : json.Reasons.Items.Where(static r => r.Length > 0).ToList(),
            Message = json.Message ?? "",
            AlternatePackage = json.AlternatePackage?.Id ?? "",
        };
    }

    private static PackageVulnerability Parse(VulnerabilityJson json)
    {
        return new PackageVulnerability
        {
            Severity = ParseSeverity(json.Severity),
            AdvisoryUrl = WebUrl.Parse(json.AdvisoryUrl),
        };
    }

    /// <summary>The severity; 0 for anything but a whole number that fits an int, or its digits.</summary>
    private static int ParseSeverity(JsonElement? severity)
    {
        if (severity is null)
        {
            return 0;
        }

        var value = severity.Value;
        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.TryGetInt32(out var number) ? number : 0;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return 0;
        }

        int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed);
        return parsed;
    }

    private static FeedResource Parse(ResourceJson json)
    {
        return new FeedResource { Types = json.Type is null ? [] : json.Type.Items, Url = WebUrl.Parse(json.Id) };
    }

    private sealed record SearchPageJson
    {
        public long? TotalHits { get; init; }

        public List<JsonElement>? Data { get; init; }
    }

    private sealed record PackageJson
    {
        public string? Id { get; init; }

        public string? Version { get; init; }

        public string? Description { get; init; }

        public TextListJson? Authors { get; init; }

        public string? IconUrl { get; init; }

        public long? TotalDownloads { get; init; }

        public bool? Verified { get; init; }

        public TextListJson? Owners { get; init; }

        public TextListJson? Tags { get; init; }

        public string? ProjectUrl { get; init; }

        public string? LicenseUrl { get; init; }

        public List<JsonElement>? Vulnerabilities { get; init; }

        public List<VersionJson?>? Versions { get; init; }
    }

    private sealed record VersionJson
    {
        public string? Version { get; init; }

        public long? Downloads { get; init; }

        [JsonPropertyName("@id")]
        public string? Id { get; init; }
    }

    private sealed record RegistrationJson
    {
        public string? Published { get; init; }

        public JsonElement? CatalogEntry { get; init; }
    }

    private sealed record CatalogEntryJson
    {
        public string? Published { get; init; }

        public string? LicenseExpression { get; init; }

        public string? LicenseUrl { get; init; }

        public string? ProjectUrl { get; init; }

        public long? PackageSize { get; init; }

        public List<DependencyGroupJson?>? DependencyGroups { get; init; }

        public DeprecationJson? Deprecation { get; init; }

        public List<VulnerabilityJson?>? Vulnerabilities { get; init; }
    }

    private sealed record DependencyGroupJson
    {
        public string? TargetFramework { get; init; }

        public List<DependencyJson?>? Dependencies { get; init; }
    }

    private sealed record DependencyJson
    {
        public string? Id { get; init; }

        public string? Range { get; init; }
    }

    private sealed record DeprecationJson
    {
        public TextListJson? Reasons { get; init; }

        public string? Message { get; init; }

        public AlternatePackageJson? AlternatePackage { get; init; }
    }

    private sealed record AlternatePackageJson(string? Id);

    /// <summary>A vulnerability, whose severity a feed may give as a number or as its digits.</summary>
    private sealed record VulnerabilityJson
    {
        public string? AdvisoryUrl { get; init; }

        public JsonElement? Severity { get; init; }
    }

    private sealed record ServiceIndexJson(List<JsonElement>? Resources);

    private sealed record ResourceJson
    {
        [JsonPropertyName("@id")]
        public string? Id { get; init; }

        [JsonPropertyName("@type")]
        public TextListJson? Type { get; init; }
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
    [JsonSerializable(typeof(SearchPageJson))]
    [JsonSerializable(typeof(PackageJson))]
    [JsonSerializable(typeof(RegistrationJson))]
    [JsonSerializable(typeof(CatalogEntryJson))]
    [JsonSerializable(typeof(ServiceIndexJson))]
    [JsonSerializable(typeof(ResourceJson))]
    private sealed partial class FeedJsonContext : JsonSerializerContext;
}
