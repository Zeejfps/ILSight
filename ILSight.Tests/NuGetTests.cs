using ILSight.Core;

namespace ILSight.Tests;

public sealed class NuGetFeedTests
{
    private const string TwoPackages = """
        { "totalHits": 2, "data": [
            { "id": "Newtonsoft.Json", "version": "13.0.3", "description": "Json.NET", "authors": ["James Newton-King"],
              "versions": [ { "version": "12.0.1" }, { "version": "13.0.3" } ] },
            { "id": "Tiny", "version": "1.0.0", "description": "Small", "authors": "Ann, Bo" } ] }
        """;

    [Fact]
    public async Task A_search_reads_each_package_with_its_versions_newest_first()
    {
        var fake = new FakeFeed();
        fake.AnswerSearches(TwoPackages);
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        var found = await NuGetFeeds.SearchAsync(feed, "json", includePrerelease: false);

        var packages = found.Packages;
        Assert.Equal(["Newtonsoft.Json", "Tiny"], packages.Select(p => p.Id.Text));
        Assert.Equal(["13.0.3", "12.0.1"], packages[0].Versions.Select(v => v.Version.Text));
        Assert.Equal("James Newton-King", packages[0].Authors);
        Assert.Equal("Ann, Bo", packages[1].Authors);
        Assert.Equal(["1.0.0"], packages[1].Versions.Select(v => v.Version.Text));
        Assert.Equal(2, found.TotalHits);
    }

    [Fact]
    public async Task A_search_leaves_out_packages_and_versions_no_folder_could_be_named_for()
    {
        var fake = new FakeFeed();
        fake.AnswerSearches("""
            { "totalHits": 4, "data": [
                { "id": "..\\..\\Evil", "version": "1.0.0" },
                { "id": "C:\\Evil", "version": "1.0.0" },
                { "id": "Evil", "version": "1.0.0\\..\\.." },
                { "id": "Tiny", "version": "2.0.0",
                  "versions": [ { "version": "1.0.0" }, { "version": "../2.0.0" }, { "version": "2.0.0" } ] } ] }
            """);
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        var package = Assert.Single((await NuGetFeeds.SearchAsync(feed, "evil", includePrerelease: false)).Packages);

        Assert.Equal("Tiny", package.Id.Text);
        Assert.Equal(["2.0.0", "1.0.0"], package.Versions.Select(v => v.Version.Text));
    }

    [Fact]
    public async Task A_search_reads_what_each_package_says_of_itself()
    {
        var fake = new FakeFeed();
        fake.AnswerSearches("""
            { "totalHits": 1, "data": [
                { "id": "Serilog", "version": "4.4.0", "totalDownloads": 3477571230, "verified": true,
                  "owners": ["serilog"], "tags": ["logging", "structured"], "projectUrl": "https://serilog.net/",
                  "vulnerabilities": [ { "advisoryUrl": "https://advisory.test", "severity": "2" } ],
                  "versions": [ { "version": "4.4.0", "downloads": 5, "@id": "https://feed.test/reg/serilog/4.4.0.json" } ] } ] }
            """);
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        var package = (await NuGetFeeds.SearchAsync(feed, "serilog", includePrerelease: false)).Packages[0];

        Assert.Equal(3477571230, package.TotalDownloads);
        Assert.True(package.Verified);
        Assert.Equal("serilog", package.Owners);
        Assert.Equal(["logging", "structured"], package.Tags);
        Assert.Equal("https://serilog.net/", package.ProjectUrl?.Text);
        Assert.Equal(1, package.Vulnerabilities);
        Assert.Equal(new FeedVersion(PackageVersion.Parse("4.4.0")!, 5, WebUrl.Parse("https://feed.test/reg/serilog/4.4.0.json")), package.Versions[0]);
    }

    [Fact]
    public async Task A_search_keeps_no_url_that_is_not_http_or_https()
    {
        var fake = new FakeFeed();
        fake.AnswerSearches("""
            { "totalHits": 1, "data": [
                { "id": "Tiny", "version": "1.0.0", "iconUrl": "javascript:alert(1)",
                  "projectUrl": "file:///C:/Windows/System32/cmd.exe", "licenseUrl": "C:\\Windows\\System32\\calc.exe",
                  "versions": [ { "version": "1.0.0", "@id": "ms-settings:" } ] } ] }
            """);
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        var package = (await NuGetFeeds.SearchAsync(feed, "tiny", includePrerelease: false)).Packages[0];

        Assert.Null(package.IconUrl);
        Assert.Null(package.ProjectUrl);
        Assert.Null(package.LicenseUrl);
        Assert.Null(package.Versions[0].DetailsUrl);
    }

    [Theory]
    [InlineData("2.5")]
    [InlineData("99999999999")]
    [InlineData("\"high\"")]
    [InlineData("null")]
    public async Task A_severity_that_is_no_whole_number_reads_as_0(string severity)
    {
        var fake = new FakeFeed();
        fake.Answer("https://feed.test/reg/tiny/1.0.0.json", $$"""
            { "catalogEntry": { "vulnerabilities": [ { "advisoryUrl": "https://advisory.test", "severity": {{severity}} } ] } }
            """);
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        var details = await NuGetFeeds.ReadDetailsAsync(feed, PackageId.Parse("Tiny")!, new FeedVersion(PackageVersion.Parse("1.0.0")!, 0, WebUrl.Parse("https://feed.test/reg/tiny/1.0.0.json")));

        Assert.Equal(0, Assert.Single(details.Vulnerabilities).Severity);
    }

    [Fact]
    public async Task A_version_s_details_are_read_from_its_catalog_entry()
    {
        var fake = new FakeFeed();
        fake.Answer("https://feed.test/reg/tiny/1.0.0.json", """
            { "published": "2024-05-01T10:00:00Z", "catalogEntry": "https://feed.test/catalog/tiny.1.0.0.json" }
            """);
        fake.Answer("https://feed.test/catalog/tiny.1.0.0.json", """
            { "licenseExpression": "MIT", "packageSize": 2048,
              "dependencyGroups": [ { "targetFramework": "net8.0", "dependencies": [ { "id": "Dep", "range": "[1.0.0, )" } ] },
                                    { "targetFramework": "netstandard2.0" } ],
              "deprecation": { "reasons": ["Legacy"], "message": "Old.", "alternatePackage": { "id": "Tiny2" } },
              "vulnerabilities": [ { "advisoryUrl": "https://advisory.test", "severity": "3" } ] }
            """);
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        var details = await NuGetFeeds.ReadDetailsAsync(feed, PackageId.Parse("Tiny")!, new FeedVersion(PackageVersion.Parse("1.0.0")!, 0, WebUrl.Parse("https://feed.test/reg/tiny/1.0.0.json")));

        Assert.Equal(new DateTimeOffset(2024, 5, 1, 10, 0, 0, TimeSpan.Zero), details.Published);
        Assert.Equal("MIT", details.License);
        Assert.Equal(2048, details.PackageSize);
        Assert.Equal(["net8.0", "netstandard2.0"], details.DependencyGroups.Select(g => g.TargetFramework));
        Assert.Equal(new PackageDependency("Dep", "[1.0.0, )"), Assert.Single(details.DependencyGroups[0].Dependencies));
        Assert.Equal("Tiny2", details.Deprecation!.AlternatePackage);
        Assert.Equal(3, Assert.Single(details.Vulnerabilities).Severity);
    }

    [Fact]
    public async Task A_package_of_another_shape_is_left_out_and_the_rest_of_the_page_kept()
    {
        var fake = new FakeFeed();
        fake.AnswerSearches("""
            { "totalHits": 3, "data": [
                { "id": "Odd", "version": "1.0.0", "description": { "text": "not a string" } },
                "not a package",
                { "id": "Tiny", "version": "1.0.0", "totalDownloads": "42", "tags": "small tiny" } ] }
            """);
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        var package = Assert.Single((await NuGetFeeds.SearchAsync(feed, "tiny", includePrerelease: false)).Packages);

        Assert.Equal("Tiny", package.Id.Text);
        Assert.Equal(42, package.TotalDownloads);
        Assert.Equal(["small", "tiny"], package.Tags);
    }

    [Theory]
    [InlineData("""[ { "catalogEntry": { "licenseExpression": "MIT" } } ]""")]
    [InlineData("""{ "catalogEntry": [] }""")]
    [InlineData("""{ "catalogEntry": "https://feed.test/catalog/array.json" }""")]
    [InlineData("""{ "catalogEntry": { "deprecation": "old" } }""")]
    public async Task Details_of_another_shape_cannot_be_read(string registration)
    {
        var fake = new FakeFeed();
        fake.Answer("https://feed.test/reg/tiny/1.0.0.json", registration);
        fake.Answer("https://feed.test/catalog/array.json", "[]");
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            NuGetFeeds.ReadDetailsAsync(feed, PackageId.Parse("Tiny")!, new FeedVersion(PackageVersion.Parse("1.0.0")!, 0, WebUrl.Parse("https://feed.test/reg/tiny/1.0.0.json"))));
    }

    [Fact]
    public async Task A_resource_may_name_its_types_as_a_list_and_one_of_another_shape_is_passed_over()
    {
        var fake = new FakeFeed();
        fake.Answer(FakeFeed.IndexUrl, $$"""
            { "version": "3.0.0", "resources": [
                "not a resource",
                { "@id": "https://feed.test/elsewhere", "@type": 3 },
                { "@id": "{{FakeFeed.SearchUrl}}", "@type": [ "SearchQueryService/3.5.0", "SearchQueryService" ] },
                { "@id": "{{FakeFeed.PackagesUrl}}", "@type": "PackageBaseAddress/3.0.0" } ] }
            """);
        fake.AnswerSearches("""{ "data": [ { "id": "Tiny", "version": "1.0.0" } ] }""");
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        var found = await NuGetFeeds.SearchAsync(feed, "tiny", includePrerelease: false);

        Assert.Equal("Tiny", Assert.Single(found.Packages).Id.Text);
    }

    [Fact]
    public async Task A_version_the_search_gave_no_address_for_is_found_through_the_registrations()
    {
        var fake = new FakeFeed();
        fake.Answer(FakeFeed.IndexUrl, """
            { "version": "3.0.0", "resources": [
                { "@id": "https://feed.test/search", "@type": "SearchQueryService" },
                { "@id": "https://feed.test/flat/", "@type": "PackageBaseAddress/3.0.0" },
                { "@id": "https://feed.test/reg/", "@type": "RegistrationsBaseUrl/3.6.0" } ] }
            """);
        fake.Answer("https://feed.test/reg/tiny/1.0.0-beta.json", """{ "catalogEntry": { "licenseUrl": "https://license.test" } }""");
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        var details = await NuGetFeeds.ReadDetailsAsync(feed, PackageId.Parse("Tiny")!, new FeedVersion(PackageVersion.Parse("1.0.0-Beta")!, 0, null));

        Assert.Equal("https://license.test", details.LicenseUrl?.Text);
        Assert.Null(details.Published);
    }

    [Theory]
    [InlineData("[8.0.1, )", "≥ 8.0.1")]
    [InlineData("8.0.1", "≥ 8.0.1")]
    [InlineData("[1.0]", "= 1.0")]
    [InlineData("[1.0, 2.0)", "≥ 1.0, < 2.0")]
    [InlineData("(, 2.0]", "≤ 2.0")]
    [InlineData("", "")]
    public void A_version_range_is_described_as_comparisons(string range, string described)
    {
        Assert.Equal(described, VersionRange.Describe(range));
    }

    [Fact]
    public async Task A_page_of_a_search_skips_the_pages_before_it()
    {
        var fake = new FakeFeed();
        fake.AnswerSearches(TwoPackages);
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        await NuGetFeeds.SearchAsync(feed, "json", includePrerelease: false, page: 2);

        Assert.Contains($"skip={2 * NuGetFeed.PageSize}&take={NuGetFeed.PageSize}&", fake.Requested.Last());
    }

    [Fact]
    public async Task A_feed_that_gives_no_total_counts_what_is_on_the_page()
    {
        var fake = new FakeFeed();
        fake.AnswerSearches("""{ "data": [ { "id": "Tiny", "version": "1.0.0" } ] }""");
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        var found = await NuGetFeeds.SearchAsync(feed, "", includePrerelease: false);

        Assert.Equal(1, found.TotalHits);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(25, 1)]
    [InlineData(26, 2)]
    [InlineData(400000, 121)]
    public void The_pages_are_counted_up_to_the_last_one_a_feed_lets_start(int totalHits, int pages)
    {
        Assert.Equal(pages, NuGetFeeds.CountPages(totalHits));
    }

    [Fact]
    public async Task A_search_asks_for_the_query_and_whether_prereleases_count()
    {
        var fake = new FakeFeed();
        fake.AnswerSearches(TwoPackages);
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        await NuGetFeeds.SearchAsync(feed, " a&b ", includePrerelease: true);

        var search = fake.Requested.Last();
        Assert.StartsWith(FakeFeed.SearchUrl + "?q=a%26b&", search);
        Assert.Contains("prerelease=true", search);
    }

    [Fact]
    public async Task The_service_index_is_read_once()
    {
        var fake = new FakeFeed();
        fake.AnswerSearches(TwoPackages);
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        await NuGetFeeds.SearchAsync(feed, "a", includePrerelease: false);
        await NuGetFeeds.SearchAsync(feed, "b", includePrerelease: false);

        Assert.Single(fake.Requested, FakeFeed.IndexUrl);
    }

    [Fact]
    public async Task A_service_index_that_failed_to_read_is_read_again_by_the_next_call()
    {
        var fake = new FakeFeed();
        fake.AnswerSearches(TwoPackages);
        fake.Answer(FakeFeed.IndexUrl, """{ "version": "3.0.0", "resources": [] }""");
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);
        await Assert.ThrowsAsync<InvalidDataException>(() => NuGetFeeds.SearchAsync(feed, "a", includePrerelease: false));

        fake.AnswerIndexAt(FakeFeed.IndexUrl);
        await NuGetFeeds.SearchAsync(feed, "b", includePrerelease: false);

        Assert.Equal(2, fake.Requested.Count(static url => url == FakeFeed.IndexUrl));
    }

    [Fact]
    public async Task A_read_of_the_service_index_cancelled_midway_leaves_it_for_the_next_call()
    {
        var fake = new FakeFeed { Holds = true };
        fake.AnswerSearches(TwoPackages);
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);
        using var cancellation = new CancellationTokenSource();
        var cancelled = NuGetFeeds.SearchAsync(feed, "a", includePrerelease: false, cancellationToken: cancellation.Token);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);

        fake.Holds = false;
        await NuGetFeeds.SearchAsync(feed, "b", includePrerelease: false);

        Assert.Equal(2, fake.Requested.Count(static url => url == FakeFeed.IndexUrl));
    }

    [Fact]
    public async Task A_feed_whose_index_names_no_search_cannot_be_searched()
    {
        var fake = new FakeFeed();
        fake.Answer(FakeFeed.IndexUrl, """{ "version": "3.0.0", "resources": [] }""");
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => NuGetFeeds.SearchAsync(feed, "a", includePrerelease: false));

        Assert.Contains("SearchQueryService", error.Message);
    }

    [Fact]
    public async Task A_feed_that_answers_with_no_json_says_so()
    {
        var fake = new FakeFeed();
        fake.AnswerSearches("<html>maintenance</html>");
        var feed = NuGetFeeds.CreateFeed(fake.Source, fake);

        await Assert.ThrowsAsync<InvalidDataException>(() => NuGetFeeds.SearchAsync(feed, "a", includePrerelease: false));
    }
}

public sealed class PackageCacheTests
{
    private static readonly byte[] PackageBytes = [1, 2, 3];
    private static readonly PackageId Tiny = PackageId.Parse("Tiny")!;
    private static readonly PackageId Gone = PackageId.Parse("Gone")!;
    private static readonly PackageVersion OneZero = PackageVersion.Parse("1.0.0")!;

    [Fact]
    public async Task A_package_not_on_disk_is_downloaded_from_its_lower_case_url_and_kept()
    {
        using var temp = new TempDir();
        var fake = new FakeFeed();
        fake.Answer(FakeFeed.PackagesUrl + "tiny/1.0.0-beta/tiny.1.0.0-beta.nupkg", PackageBytes);
        var cache = new PackageCache { GlobalPackagesFolder = temp.PathOf("global"), DownloadFolder = temp.PathOf("downloads") };

        var path = await NuGetFeeds.FetchPackageAsync(cache, NuGetFeeds.CreateFeed(fake.Source, fake), Tiny, PackageVersion.Parse("1.0.0-Beta+build.5")!);

        Assert.Equal(temp.PathOf(Path.Combine("downloads", "tiny", "1.0.0-beta", "tiny.1.0.0-beta.nupkg")), path);
        Assert.Equal(PackageBytes, File.ReadAllBytes(path));
        Assert.Equal(path, NuGetFeeds.FindPackage(cache, Tiny, PackageVersion.Parse("1.0.0-beta")!));
    }

    [Fact]
    public async Task A_package_a_restore_put_in_the_global_folder_is_opened_from_there()
    {
        using var temp = new TempDir();
        var restored = temp.Write(Path.Combine("global", "tiny", "1.0.0", "tiny.1.0.0.nupkg"), PackageBytes);
        var fake = new FakeFeed();
        var cache = new PackageCache { GlobalPackagesFolder = temp.PathOf("global"), DownloadFolder = temp.PathOf("downloads") };

        var path = await NuGetFeeds.FetchPackageAsync(cache, NuGetFeeds.CreateFeed(fake.Source, fake), Tiny, OneZero);

        Assert.Equal(restored, path);
        Assert.Empty(fake.Requested);
    }

    [Fact]
    public async Task A_package_the_feed_does_not_have_leaves_nothing_behind()
    {
        using var temp = new TempDir();
        var fake = new FakeFeed();
        var cache = new PackageCache { GlobalPackagesFolder = temp.PathOf("global"), DownloadFolder = temp.PathOf("downloads") };

        await Assert.ThrowsAsync<HttpRequestException>(() => NuGetFeeds.FetchPackageAsync(cache, NuGetFeeds.CreateFeed(fake.Source, fake), Gone, OneZero));

        Assert.Null(NuGetFeeds.FindPackage(cache, Gone, OneZero));
        Assert.False(File.Exists(temp.PathOf(Path.Combine("downloads", "gone", "1.0.0", "gone.1.0.0.nupkg.download"))));
    }
}

public sealed class PackageIdTests
{
    [Theory]
    [InlineData("Newtonsoft.Json")]
    [InlineData("Microsoft.Extensions.DependencyInjection")]
    [InlineData("my_package-2")]
    public void An_id_NuGet_allows_parses(string text)
    {
        Assert.Equal(text, PackageId.Parse(text)?.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("..\\x")]
    [InlineData("../x")]
    [InlineData("C:\\x")]
    [InlineData("/x")]
    [InlineData("a..b")]
    [InlineData(".a")]
    [InlineData("a.")]
    [InlineData("a\n")]
    public void An_id_that_could_name_another_folder_does_not_parse(string text)
    {
        Assert.Null(PackageId.Parse(text));
    }

    [Fact]
    public void An_id_longer_than_NuGet_allows_does_not_parse()
    {
        Assert.Null(PackageId.Parse(new string('a', 101)));
    }
}

public sealed class PackageVersionTests
{
    [Theory]
    [InlineData("1.0", "1.0")]
    [InlineData("1.0.0.0", "1.0.0.0")]
    [InlineData("1.0.0-Beta.2", "1.0.0-beta.2")]
    [InlineData("1.0.0-rc+build.5", "1.0.0-rc")]
    public void A_version_NuGet_allows_parses_and_is_filed_in_lower_case_without_its_metadata(string text, string filed)
    {
        Assert.Equal(filed, PackageVersion.Parse(text)?.Filed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.0.0.0.0")]
    [InlineData("..")]
    [InlineData("1.0/../..")]
    [InlineData("1.0.0-..")]
    [InlineData("1.0.0\\x")]
    [InlineData("1.0\n")]
    public void A_version_that_could_name_another_folder_does_not_parse(string text)
    {
        Assert.Null(PackageVersion.Parse(text));
    }
}

public sealed class PackageIconsTests
{
    private const string IconUrl = "https://feed.test/icon";

    private static WebUrl Icon => WebUrl.Parse(IconUrl)!;

    [Fact]
    public async Task An_icon_is_fetched_and_drawn_to_fit_the_side_asked_for()
    {
        var fake = new FakeFeed();
        fake.Answer(IconUrl, """<svg xmlns="http://www.w3.org/2000/svg" width="256" height="128"><rect width="256" height="128" fill="red"/></svg>""");
        var icons = NuGetFeeds.CreateIcons(fake);

        var icon = await NuGetFeeds.FetchIconAsync(icons, Icon, 64);

        Assert.Equal(64, icon.Width);
        Assert.Equal(32, icon.Height);
    }

    [Fact]
    public async Task What_is_no_picture_cannot_be_an_icon()
    {
        var fake = new FakeFeed();
        fake.Answer(IconUrl, "<html>not found</html>");
        var icons = NuGetFeeds.CreateIcons(fake);

        await Assert.ThrowsAsync<InvalidDataException>(() => NuGetFeeds.FetchIconAsync(icons, Icon, 64));
    }

    [Fact]
    public void A_picture_shrinks_by_averaging_what_each_pixel_covers_and_clear_pixels_lend_no_color()
    {
        byte[] rgba =
        [
            255, 0, 0, 255, 0, 0, 255, 255, 0, 255, 0, 0, 0, 255, 0, 0,
            255, 0, 0, 255, 0, 0, 255, 255, 0, 255, 0, 0, 0, 255, 0, 0,
        ];
        var image = new ILSight.Core.Resources.ResourceImage { Width = 4, Height = 2, Rgba = rgba, Format = "PNG" };

        var shrunk = ILSight.Core.Resources.ResourceImages.FitInto(image, 2);

        Assert.Equal(2, shrunk.Width);
        Assert.Equal(1, shrunk.Height);
        Assert.Equal([127, 0, 127, 255, 0, 0, 0, 0], shrunk.Rgba);
    }
}

public sealed class NuGetSetupTests
{
    [Fact]
    public void A_typed_url_is_a_source_named_after_its_host()
    {
        var source = NuGetSource.FromUrl(" https://pkgs.test/v3/index.json ");

        Assert.Equal(("pkgs.test", "https://pkgs.test/v3/index.json"), (source?.Name, source?.Url.Text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nuget.org")]
    [InlineData("C:\\packages")]
    [InlineData("ftp://pkgs.test/index.json")]
    public void Text_that_is_no_web_url_is_no_source(string typed)
    {
        Assert.Null(NuGetSource.FromUrl(typed));
    }

    [Fact]
    public void A_feed_is_made_once_per_url_and_named_as_the_config_names_it()
    {
        using var temp = new TempDir();
        var listed = NuGetSource.FromConfig("company", "https://pkgs.test/v3/index.json")!;
        var setup = NuGetFeeds.CreateSetup([listed], new PackageCache { GlobalPackagesFolder = temp.PathOf("global"), DownloadFolder = temp.PathOf("downloads") });

        var feed = NuGetFeeds.EnsureFeed(setup, NuGetSource.FromUrl("https://PKGS.test/v3/index.json")!);

        Assert.Same(feed, NuGetFeeds.EnsureFeed(setup, listed));
        Assert.Equal("company", feed.Source.Name);
    }
}

public sealed class NuGetConfigTests
{
    [Fact]
    public void The_enabled_v3_sources_are_listed_in_order()
    {
        using var temp = new TempDir();
        var path = temp.Write("NuGet.Config", """
            <configuration>
              <packageSources>
                <add key="old" value="https://old.test/v3/index.json" />
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                <add key="v2" value="https://v2.test/api/v2" />
                <add key="local" value="C:\packages" />
                <add key="off" value="https://off.test/v3/index.json" />
                <add key="company" value="https://company.test/nuget/v3/index.json" />
              </packageSources>
              <disabledPackageSources>
                <add key="off" value="true" />
              </disabledPackageSources>
            </configuration>
            """);

        var config = NuGetConfigs.Read(path, null);

        Assert.Equal(["nuget.org", "company"], config.Sources.Select(s => s.Name));
        Assert.Equal("https://company.test/nuget/v3/index.json", config.Sources[1].Url.Text);
    }

    [Fact]
    public void A_missing_config_offers_nuget_org()
    {
        using var temp = new TempDir();

        var config = NuGetConfigs.Read(temp.PathOf("NuGet.Config"), null);

        Assert.Equal([NuGetSource.NuGetOrg], config.Sources);
    }

    [Fact]
    public void A_config_that_is_no_xml_offers_nuget_org()
    {
        using var temp = new TempDir();
        var path = temp.Write("NuGet.Config", "not xml");

        var config = NuGetConfigs.Read(path, null);

        Assert.Equal([NuGetSource.NuGetOrg], config.Sources);
    }

    [Fact]
    public void The_global_packages_folder_the_config_names_is_found_from_the_config_s_folder()
    {
        using var temp = new TempDir();
        var path = WriteConfigNamingRestored(temp);

        var config = NuGetConfigs.Read(path, null);

        Assert.Equal(temp.PathOf("restored"), config.GlobalPackagesFolder);
    }

    [Fact]
    public void An_absolute_nuget_packages_folder_comes_before_the_config_s()
    {
        using var temp = new TempDir();
        var path = WriteConfigNamingRestored(temp);
        var fromEnvironment = Path.Combine(temp.Path, "env", "..", "elsewhere");

        var config = NuGetConfigs.Read(path, fromEnvironment);

        Assert.Equal(temp.PathOf("elsewhere"), config.GlobalPackagesFolder);
    }

    [Theory]
    [InlineData("packages")]
    [InlineData(@"%USERPROFILE%\.nuget\packages")]
    [InlineData("")]
    public void A_nuget_packages_folder_that_is_no_absolute_path_is_passed_over_for_the_config_s(string fromEnvironment)
    {
        using var temp = new TempDir();
        var path = WriteConfigNamingRestored(temp);

        var config = NuGetConfigs.Read(path, fromEnvironment);

        Assert.Equal(temp.PathOf("restored"), config.GlobalPackagesFolder);
    }

    private static string WriteConfigNamingRestored(TempDir temp)
    {
        return temp.Write("NuGet.Config", """
        <configuration>
          <config>
            <add key="globalPackagesFolder" value="restored" />
          </config>
        </configuration>
        """);
    }
}
