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
