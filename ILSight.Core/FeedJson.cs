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
        return new FeedPage(packages, Math.Max(total, packages.Count));
    }

    /// <exception cref="InvalidDataException">It is no registration of a version.</exception>
    public static FeedRegistration ReadRegistration(JsonElement root)
    {
        var registration = Read(root, FeedJsonContext.Default.RegistrationJson, "a registration");
        return new FeedRegistration(ReadDate(registration.Published), registration.CatalogEntry);
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

        return new FeedPackage(id, version, json.Description ?? "", JoinNames(json.Authors), WebUrl.Parse(json.IconUrl), ListVersions(json.Versions, version))
        {
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
            versions.Add(new FeedVersion(latest, 0, null));
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

        return new FeedVersion(version, json.Downloads ?? 0, WebUrl.Parse(json.Id));
    }

    private static PackageVersionDetails Parse(CatalogEntryJson json, DateTimeOffset? listedAt)
    {
        return new PackageVersionDetails(
            ReadDate(json.Published) ?? listedAt,
            json.LicenseExpression ?? "",
            WebUrl.Parse(json.LicenseUrl),
            WebUrl.Parse(json.ProjectUrl),
            json.PackageSize ?? 0,
            (json.DependencyGroups ?? []).OfType<DependencyGroupJson>().Select(static g => Parse(g)).ToList(),
            json.Deprecation is null ? null : Parse(json.Deprecation),
            (json.Vulnerabilities ?? []).OfType<VulnerabilityJson>().Select(static v => Parse(v)).ToList());
    }

    private static DependencyGroup Parse(DependencyGroupJson json)
    {
        return new DependencyGroup(
            json.TargetFramework ?? "",
            (json.Dependencies ?? []).OfType<DependencyJson>().Select(static d => new PackageDependency(d.Id ?? "", d.Range ?? "")).ToList());
    }

    private static PackageDeprecation Parse(DeprecationJson json)
    {
        return new PackageDeprecation(
            json.Reasons is null ? [] : json.Reasons.Items.Where(static r => r.Length > 0).ToList(),
            json.Message ?? "",
            json.AlternatePackage?.Id ?? "");
    }

    private static PackageVulnerability Parse(VulnerabilityJson json)
    {
        return new PackageVulnerability(ParseSeverity(json.Severity), WebUrl.Parse(json.AdvisoryUrl));
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
        return new FeedResource(json.Type is null ? [] : json.Type.Items, WebUrl.Parse(json.Id));
    }

    private sealed record SearchPageJson(long? TotalHits, List<JsonElement>? Data);

    private sealed record PackageJson(
        string? Id,
        string? Version,
        string? Description,
        TextListJson? Authors,
        string? IconUrl,
        long? TotalDownloads,
        bool? Verified,
        TextListJson? Owners,
        TextListJson? Tags,
        string? ProjectUrl,
        string? LicenseUrl,
        List<JsonElement>? Vulnerabilities,
        List<VersionJson?>? Versions);

    private sealed record VersionJson(string? Version, long? Downloads, [property: JsonPropertyName("@id")] string? Id);

    private sealed record RegistrationJson(string? Published, JsonElement? CatalogEntry);

    private sealed record CatalogEntryJson(
        string? Published,
        string? LicenseExpression,
        string? LicenseUrl,
        string? ProjectUrl,
        long? PackageSize,
        List<DependencyGroupJson?>? DependencyGroups,
        DeprecationJson? Deprecation,
        List<VulnerabilityJson?>? Vulnerabilities);

    private sealed record DependencyGroupJson(string? TargetFramework, List<DependencyJson?>? Dependencies);

    private sealed record DependencyJson(string? Id, string? Range);

    private sealed record DeprecationJson(TextListJson? Reasons, string? Message, AlternatePackageJson? AlternatePackage);

    private sealed record AlternatePackageJson(string? Id);

    /// <summary>A vulnerability, whose severity a feed may give as a number or as its digits.</summary>
    private sealed record VulnerabilityJson(string? AdvisoryUrl, JsonElement? Severity);

    private sealed record ServiceIndexJson(List<JsonElement>? Resources);

    private sealed record ResourceJson([property: JsonPropertyName("@id")] string? Id, [property: JsonPropertyName("@type")] TextListJson? Type);

    /// <summary>Text a feed may give as one string or as a list of strings; what else a list holds is left out.</summary>
    [JsonConverter(typeof(TextListConverter))]
    private sealed record TextListJson(IReadOnlyList<string> Items);

    private sealed class TextListConverter : JsonConverter<TextListJson>
    {
        public override TextListJson Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new TextListJson([reader.GetString() ?? ""]);
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Expected a string or a list of them.");
            }

            var items = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    items.Add(reader.GetString() ?? "");
                }
                else
                {
                    reader.Skip();
                }
            }
            return new TextListJson(items);
        }

        public override void Write(Utf8JsonWriter writer, TextListJson value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var item in value.Items)
            {
                writer.WriteStringValue(item);
            }

            writer.WriteEndArray();
        }
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
