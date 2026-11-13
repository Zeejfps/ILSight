using System.IO.Compression;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.NuGetArea;
using ILSight.ILSightAppArea;

namespace ILSight.App.Tests;

public sealed class NuGetPickerTests : IDisposable
{
    private const string FixturesPackage = """
        { "totalHits": 1, "data": [
            { "id": "Fixtures", "version": "2.0.0", "description": "The fixtures.", "authors": ["Zee"],
              "versions": [ { "version": "1.0.0" }, { "version": "2.0.0" } ] } ] }
        """;

    private const string NoPackages = """{ "totalHits": 0, "data": [] }""";

    private const string ThreePagesOfPackages = """
        { "totalHits": 60, "data": [ { "id": "Fixtures", "version": "2.0.0" } ] }
        """;

    private const string OtherIndexUrl = "https://other.test/v3/index.json";

    private const string IconUrl = "https://feed.test/fixtures/icon";

    private const string PackageWithIcon = $$"""
        { "totalHits": 1, "data": [
            { "id": "Fixtures", "version": "2.0.0", "description": "The fixtures.", "iconUrl": "{{IconUrl}}" } ] }
        """;

    private const string DetailsUrl = "https://feed.test/reg/fixtures/2.0.0.json";

    private const string PackageWithDetails = $$"""
        { "totalHits": 1, "data": [
            { "id": "Fixtures", "version": "2.0.0", "description": "The fixtures.", "authors": ["Zee"], "owners": ["zee"],
              "totalDownloads": 1234567, "verified": true,
              "versions": [ { "version": "2.0.0", "downloads": 10, "@id": "{{DetailsUrl}}" } ] } ] }
        """;

    private const string SquareSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="128" height="128"><rect width="128" height="128" fill="#3366ff"/></svg>
        """;

    private readonly TempDir _temp = new();
    private readonly FakeFeed _fake = new();
    private readonly List<string> _reported = new();
    private readonly List<Task> _started = [];
    private readonly NuGetClient _client;

    public NuGetPickerTests()
    {
        _client = new NuGetClient(_fake);
        _fake.AnswerSearches(FixturesPackage);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    private NuGetSetup Setup()
    {
        var cache = new PackageCache { GlobalPackagesFolder = _temp.PathOf("global"), DownloadFolder = _temp.PathOf("downloads") };
        return new NuGetSetup { Sources = [_fake.Source], Cache = cache };
    }

    private void Report(AppProblem problem)
    {
        _reported.Add(ProblemTexts.Describe(Strings.En, problem).Title);
    }

    private SearchNuGetFeedHandler CreateSearchFeed()
    {
        return new SearchNuGetFeedHandler(new SearchNuGetFeedHandlerAdapter(Report, _client));
    }

    private DownloadNuGetPackageHandler CreateDownloadPackage()
    {
        return new DownloadNuGetPackageHandler(new DownloadNuGetPackageHandlerAdapter(Report, _client));
    }

    private NuGetPicker OpenPicker(PostedActions posted, SavedNuGetSearch? saved = null)
    {
        var picker = new NuGetPicker { Browser = new NuGetBrowser() };
        picker.RestoreSearch(saved);
        picker.UseSetup(Setup());
        picker.Open();
        picker.Browser.StopUnlessOpen(picker.IsOpen);
        Update(picker, now: 10);
        Poll.Until(() => !picker.Browser.IsSearching, posted.Run);
        picker.Browser.FollowSelection();
        return picker;
    }

    private double Update(NuGetPicker picker, double now)
    {
        using var turn = PostedActions.OfThisTest().Install();
        var wanted = picker.Update(now);
        if (wanted is not null)
        {
            var search = picker.Browser.StartSearch(wanted);
            _started.Add(CreateSearchFeed().HandleAsync(picker.Browser, search, search.Work.Token));
        }

        if (!picker.Browser.IsSettling)
        {
            return 0;
        }

        return picker.Browser.CalculateSecondsToSettle(now);
    }

    private void Start(Task workflow)
    {
        _started.Add(workflow);
    }

    private void StartDownload(NuGetPicker picker)
    {
        using var turn = PostedActions.OfThisTest().Install();
        var download = picker.Browser.StartDownload();
        Assert.NotNull(download);
        Start(CreateDownloadPackage().HandleAsync(picker.Browser, download, download.Work.Token));
    }

    private static void Close(NuGetPicker picker)
    {
        picker.IsOpen = false;
        picker.Browser.StopUnlessOpen(picker.IsOpen);
    }

    private int SearchCount()
    {
        return _fake.Requested.Count(r => r.StartsWith(FakeFeed.SearchUrl));
    }

    [Fact]
    public void Opening_searches_the_first_feed_and_picks_the_latest_version_of_the_first_package()
    {
        var posted = PostedActions.OfThisTest();

        var picker = OpenPicker(posted);

        Assert.Equal("Fixtures", Assert.Single(picker.Browser.Results).Id.Text);
        Assert.Equal("Fixtures", picker.Browser.Selected?.Id.Text);
        Assert.Equal("2.0.0", picker.Browser.Version?.Text);
        var choice = picker.Browser.Choice;
        Assert.NotNull(choice);
        Assert.Equal(["2.0.0", "1.0.0"], choice.Texts.ToArray());
        Assert.True(picker.Browser.CanOpen);
    }

    [Fact]
    public void Picking_a_listed_version_keeps_the_versions_offered()
    {
        var posted = PostedActions.OfThisTest();
        var picker = OpenPicker(posted);
        var older = Packages.Version("1.0.0");

        picker.Browser.PickVersion(older);

        Assert.Equal("1.0.0", picker.Browser.Version?.Text);
        var choice = picker.Browser.Choice;
        Assert.NotNull(choice);
        Assert.Equal("Fixtures", choice.Package.Id.Text);
        Assert.Equal(["2.0.0", "1.0.0"], choice.Texts.ToArray());
    }

    [Fact]
    public void Picking_a_version_the_package_does_not_list_throws_and_keeps_the_pick()
    {
        var posted = PostedActions.OfThisTest();
        var picker = OpenPicker(posted);
        var unlisted = Packages.Version("9.9.9");

        Assert.Throws<ArgumentException>(() => picker.Browser.PickVersion(unlisted));

        Assert.Equal("2.0.0", picker.Browser.Version?.Text);
    }

    [Fact]
    public void Following_no_selection_offers_no_versions()
    {
        var posted = PostedActions.OfThisTest();
        var picker = OpenPicker(posted);

        ItemSelections.Clear(picker.Browser.Selection);
        picker.Browser.FollowSelection();

        Assert.Null(picker.Browser.Choice);
        Assert.Null(picker.Browser.Version);
        Assert.False(picker.Browser.CanOpen);
    }

    [Fact]
    public void A_typed_query_is_searched_once_it_settles()
    {
        var posted = PostedActions.OfThisTest();
        var picker = OpenPicker(posted);

        picker.Query = "fix";
        var wait = Update(picker, now: 20);
        var searchedWhileTyping = SearchCount();
        Update(picker, now: 21);
        Poll.Until(() => !picker.Browser.IsSearching, posted.Run);

        Assert.True(wait > 0);
        Assert.Equal(1, searchedWhileTyping);
        Assert.Equal(2, SearchCount());
        Assert.Contains("q=fix", _fake.Requested.Last());
    }

    [Fact]
    public void Counting_prereleases_searches_again_at_once()
    {
        var posted = PostedActions.OfThisTest();
        var picker = OpenPicker(posted);

        picker.IncludePrerelease = true;
        var wait = Update(picker, now: 10);
        Poll.Until(() => !picker.Browser.IsSearching, posted.Run);

        Assert.Equal(0, wait);
        Assert.Contains("prerelease=true", _fake.Requested.Last());
    }

    [Fact]
    public void The_next_page_is_searched_at_once_and_a_new_query_starts_on_the_first()
    {
        var posted = PostedActions.OfThisTest();
        _fake.AnswerSearches(ThreePagesOfPackages);
        var picker = OpenPicker(posted);

        picker.Browser.ShowNextPage();
        var wait = Update(picker, now: 10);
        Poll.Until(() => !picker.Browser.IsSearching, posted.Run);
        var nextPage = _fake.Requested.Last();
        picker.Query = "fix";
        Update(picker, now: 20);
        Update(picker, now: 21);
        Poll.Until(() => !picker.Browser.IsSearching, posted.Run);

        Assert.Equal(0, wait);
        Assert.Contains($"skip={NuGetFeed.PageSize}&", nextPage);
        Assert.Equal(3, picker.Browser.PageCount);
        Assert.Equal(0, picker.Browser.Page);
        Assert.Contains("skip=0&", _fake.Requested.Last());
    }

    [Fact]
    public void A_typed_feed_url_is_searched_once_it_settles_and_kept()
    {
        var posted = PostedActions.OfThisTest();
        _fake.AnswerIndexAt(OtherIndexUrl);
        var picker = OpenPicker(posted);

        picker.FeedUrl = OtherIndexUrl;
        var wait = Update(picker, now: 20);
        Update(picker, now: 21);
        Poll.Until(() => !picker.Browser.IsSearching, posted.Run);

        Assert.True(wait > 0);
        Assert.Contains(OtherIndexUrl, _fake.Requested);
        Assert.Equal(new SavedNuGetSearch
        {
            FeedUrl = OtherIndexUrl,
            IncludePrerelease = false,
        }, picker.Browser.KeptSearch);
    }

    [Fact]
    public void A_feed_that_is_no_url_shows_nothing_and_is_not_searched()
    {
        var posted = PostedActions.OfThisTest();
        var picker = OpenPicker(posted);
        var requests = _fake.Requested.Count;

        picker.FeedUrl = "api.nuget";
        Update(picker, now: 20);
        Update(picker, now: 21);

        Assert.False(picker.HasFeedUrl);
        Assert.Empty(picker.Browser.Results);
        Assert.Null(picker.Browser.Selected);
        Assert.Equal(requests, _fake.Requested.Count);
    }

    [Fact]
    public void The_kept_feed_and_prerelease_choice_are_searched_in_place_of_the_config_s_first()
    {
        var posted = PostedActions.OfThisTest();
        _fake.AnswerIndexAt(OtherIndexUrl);

        var picker = OpenPicker(posted, new SavedNuGetSearch { FeedUrl = OtherIndexUrl, IncludePrerelease = true });

        Assert.Equal(OtherIndexUrl, picker.FeedUrl);
        Assert.DoesNotContain(FakeFeed.IndexUrl, _fake.Requested);
        Assert.Contains("prerelease=true", _fake.Requested.Last());
    }

    [Fact]
    public void Enter_while_a_query_settles_searches_it_instead_of_opening_what_the_last_one_found()
    {
        var posted = PostedActions.OfThisTest();
        var picker = OpenPicker(posted);

        picker.Query = "fix";
        Update(picker, now: 20);
        var download = picker.Browser.Submit();
        var wait = Update(picker, now: 20.01);
        Poll.Until(() => !picker.Browser.IsSearching, posted.Run);

        Assert.Null(download);
        Assert.False(picker.Browser.IsDownloading);
        Assert.Equal(0, wait);
        Assert.Contains("q=fix", _fake.Requested.Last());
    }

    [Fact]
    public void What_an_older_query_found_cannot_be_opened_while_a_new_one_settles_or_runs()
    {
        var posted = PostedActions.OfThisTest();
        var picker = OpenPicker(posted);
        _fake.Holds = true;

        picker.Query = "fix";
        Update(picker, now: 20);
        var whileSettling = picker.Browser.CanOpen;
        Update(picker, now: 21);
        var whileSearching = picker.Browser.CanOpen;
        Close(picker);

        Assert.False(whileSettling);
        Assert.False(whileSearching);
    }

    [Fact]
    public void Details_that_fail_to_load_are_read_again_on_retry()
    {
        var posted = PostedActions.OfThisTest();
        _fake.AnswerSearches(PackageWithDetails);
        var picker = OpenPicker(posted);

        var failed = ReadDetails(picker, posted);
        _fake.Answer(DetailsUrl, """{ "catalogEntry": { "licenseExpression": "MIT" } }""");
        var selected = picker.Browser.Selected;
        Assert.NotNull(selected);
        picker.Browser.RetryDetails(selected, Packages.Version("2.0.0"));
        var retried = ReadDetails(picker, posted);

        Assert.IsType<VersionDetails.Failed>(failed);
        Assert.Equal("MIT", Assert.IsType<VersionDetails.Read>(retried).Details.License);
    }

    private VersionDetails ReadDetails(NuGetPicker picker, PostedActions posted)
    {
        VersionDetails? DetailsNow()
        {
            var selected = picker.Browser.Selected;
            Assert.NotNull(selected);
            return picker.Browser.EnsureDetails(selected, Packages.Version("2.0.0"));
        }
        Assert.IsType<VersionDetails.Loading>(DetailsNow());
        var work = new BackgroundWork();
        using (posted.Install())
        {
            AppFrames.StartFetching(new WorkflowRunner(work, new ErrorLog()), new FetchNuGetIconHandler(new FetchNuGetIconHandlerAdapter(_client)),
                new ReadNuGetDetailsHandler(new ReadNuGetDetailsHandlerAdapter(_client)), picker.Browser);
        }

        Poll.Until(() => work.Outstanding.Count == 0, posted.Run);
        var details = DetailsNow();
        Assert.NotNull(details);
        return details;
    }

    [Fact]
    public void A_page_that_fails_to_load_leaves_the_page_shown_until_Retry_loads_it()
    {
        var posted = PostedActions.OfThisTest();
        _fake.AnswerSearches(ThreePagesOfPackages);
        var picker = OpenPicker(posted);

        _fake.AnswerSearches("not json");
        picker.Browser.ShowNextPage();
        Update(picker, now: 20);
        Poll.Until(() => !picker.Browser.IsSearching, posted.Run);
        var shownAfterFailure = (picker.Browser.Page, picker.Browser.Results.Count, picker.Browser.SearchError is not null);
        _fake.AnswerSearches(ThreePagesOfPackages);
        picker.Browser.RetrySearch();
        Update(picker, now: 21);
        Poll.Until(() => !picker.Browser.IsSearching, posted.Run);

        Assert.Equal((0, 1, true), shownAfterFailure);
        Assert.Equal(1, picker.Browser.Page);
        Assert.Null(picker.Browser.SearchError);
    }

    [Fact]
    public void A_new_query_that_fails_shows_nothing_from_the_last_one()
    {
        var posted = PostedActions.OfThisTest();
        var picker = OpenPicker(posted);

        _fake.AnswerSearches("not json");
        picker.Query = "other";
        Update(picker, now: 20);
        Update(picker, now: 21);
        Poll.Until(() => !picker.Browser.IsSearching, posted.Run);

        Assert.Empty(picker.Browser.Results);
        Assert.NotNull(picker.Browser.SearchError);
    }

    [Fact]
    public void The_details_of_the_version_picked_are_read_once_from_the_feed_s_catalog()
    {
        var posted = PostedActions.OfThisTest();
        _fake.AnswerSearches(PackageWithDetails);
        _fake.Answer(DetailsUrl, """{ "catalogEntry": { "licenseExpression": "MIT" } }""");
        var picker = OpenPicker(posted);

        var details = ReadDetails(picker, posted);
        var selected = picker.Browser.Selected;
        Assert.NotNull(selected);
        var again = picker.Browser.EnsureDetails(selected, Packages.Version("2.0.0"));

        Assert.Equal("MIT", Assert.IsType<VersionDetails.Read>(details).Details.License);
        Assert.Same(details, again);
        Assert.Single(_fake.Requested, DetailsUrl);
    }

    [Fact]
    public void A_search_that_finds_nothing_leaves_nothing_to_open()
    {
        var posted = PostedActions.OfThisTest();
        _fake.AnswerSearches(NoPackages);

        var picker = OpenPicker(posted);

        Assert.Empty(picker.Browser.Results);
        Assert.Null(picker.Browser.SearchError);
        Assert.False(picker.Browser.CanOpen);
    }

    [Fact]
    public void A_failed_search_says_why_and_reports_it()
    {
        var posted = PostedActions.OfThisTest();
        _fake.AnswerSearches("not json");

        var picker = OpenPicker(posted);

        Assert.NotNull(picker.Browser.SearchError);
        Assert.Equal(["Cannot search test"], _reported);
    }

    [Fact]
    public void Opening_the_picked_version_downloads_it_and_hands_its_file_over_once()
    {
        var posted = PostedActions.OfThisTest();
        _fake.Answer(FakeFeed.PackagesUrl + "fixtures/1.0.0/fixtures.1.0.0.nupkg", [1, 2, 3]);
        var picker = OpenPicker(posted);

        var older = Packages.Version("1.0.0");
        picker.Browser.PickVersion(older);
        StartDownload(picker);
        Poll.Until(() => !picker.Browser.IsDownloading, posted.Run);

        var downloaded = picker.Browser.TakeDownloaded();
        Assert.Equal(_temp.PathOf(Path.Combine("downloads", "fixtures", "1.0.0", "fixtures.1.0.0.nupkg")), downloaded);
        Assert.Null(picker.Browser.TakeDownloaded());
    }

    [Fact]
    public void A_failed_download_hands_nothing_over_and_reports_it()
    {
        var posted = PostedActions.OfThisTest();
        var picker = OpenPicker(posted);

        StartDownload(picker);
        Poll.Until(() => !picker.Browser.IsDownloading, posted.Run);

        Assert.Null(picker.Browser.TakeDownloaded());
        Assert.Equal(["Cannot download Fixtures 2.0.0"], _reported);
    }

    [Fact]
    public void Closing_while_downloading_drops_the_download()
    {
        var posted = PostedActions.OfThisTest();
        _fake.Answer(FakeFeed.PackagesUrl + "fixtures/2.0.0/fixtures.2.0.0.nupkg", [1, 2, 3]);
        var picker = OpenPicker(posted);
        _fake.Holds = true;

        StartDownload(picker);
        Close(picker);
        Poll.Until(() => _fake.Cancelled > 0, posted.Run);

        Assert.False(picker.Browser.IsDownloading);
        Assert.Null(picker.Browser.TakeDownloaded());
        Assert.Empty(_reported);
    }

    [Fact]
    public void Enter_in_the_dialog_opens_the_package_and_closes_it()
    {
        var app = new AppDriver(nuGetRequests: _fake);
        _fake.Answer(FakeFeed.PackagesUrl + "fixtures/2.0.0/fixtures.2.0.0.nupkg", File.ReadAllBytes(WritePackage()));
        var picker = app.Ui.NuGetPicker;
        picker.UseSetup(Setup());

        picker.Open();
        app.Frames.WaitUntil(() => picker.Browser.Results.Count > 0, AppStateDriver.Patience);
        app.Frames.Key(KeyboardKey.Enter);
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count > 0, AppStateDriver.Patience);
        app.Settle(2);

        Assert.False(picker.IsOpen);
        Assert.Equal(NodeIcon.Package, Assert.Single(app.Ui.Tree.Roots).Icon);
        Assert.True(app.Frames.Drew("fixtures.2.0.0.nupkg"));
    }

    [Fact]
    public void Enter_says_it_is_downloading_in_the_frame_it_was_pressed_and_closing_drops_it_quietly()
    {
        var app = new AppDriver(nuGetRequests: _fake);
        var picker = app.Ui.NuGetPicker;
        picker.UseSetup(Setup());
        picker.Open();
        app.Frames.WaitUntil(() => picker.Browser.Results.Count > 0, AppStateDriver.Patience);
        app.Run(2);
        _fake.Holds = true;
        var downloading = Strings.En.NugetDownloading("Fixtures 2.0.0");
        var before = app.Frames.Drew(downloading);

        app.Frames.Key(KeyboardKey.Enter).Frame();
        var inThatFrame = app.Frames.Drew(downloading);
        app.Frames.Key(KeyboardKey.Escape).Frame();
        app.Frames.WaitUntil(() => _fake.Cancelled > 0, AppStateDriver.Patience);
        app.Settle(2);

        Assert.False(before);
        Assert.True(inThatFrame);
        Assert.False(picker.IsOpen);
        Assert.False(picker.Browser.IsDownloading);
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public async Task The_adapter_lets_the_cancellation_of_its_token_through_rather_than_failing()
    {
        var adapter = new SearchNuGetFeedHandlerAdapter(Report, new NuGetClient(_fake));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.SearchAsync(new FeedSearch
        {
            Feed = _fake.Source,
            Query = "",
            IncludePrerelease = false,
            Page = 0,
        }, cancelled.Token));
        Assert.Empty(_reported);
    }

    [Fact]
    public void The_details_offer_the_selected_package_s_versions_and_Open_downloads_the_one_picked()
    {
        var app = new AppDriver(nuGetRequests: _fake);
        _fake.Answer(FakeFeed.PackagesUrl + "fixtures/1.0.0/fixtures.1.0.0.nupkg", File.ReadAllBytes(WritePackage()));
        var picker = app.Ui.NuGetPicker;
        picker.UseSetup(Setup());

        picker.Open();
        app.Frames.WaitUntil(() => picker.Browser.Results.Count > 0, AppStateDriver.Patience);
        app.Run(2);
        app.Frames.ClickOn(Selector.AtPath("Fixtures/version")).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "1.0.0")).Frame();
        var picked = picker.Browser.Version;
        app.Frames.ClickOn(Selector.AtPath("nuget-open")).Frame();
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count > 0, AppStateDriver.Patience);
        app.Settle(2);

        Assert.Equal("1.0.0", picked?.Text);
        Assert.False(picker.IsOpen);
        Assert.True(app.Frames.Drew("fixtures.1.0.0.nupkg"));
    }

    [Fact]
    public void The_dialog_lists_what_was_found_with_the_selected_package_s_description()
    {
        var app = new AppDriver(nuGetRequests: _fake);
        var picker = app.Ui.NuGetPicker;
        picker.UseSetup(Setup());

        picker.Open();
        app.Frames.WaitUntil(() => picker.Browser.Results.Count > 0, AppStateDriver.Patience);
        app.Run(2);

        Assert.True(app.Frames.Drew("Open from NuGet"));
        Assert.True(app.Frames.Drew("Fixtures"));
        Assert.True(app.Frames.Drew("The fixtures."));
        Assert.True(app.Frames.Drew("by Zee"));
    }

    [Fact]
    public void Next_in_the_dialog_shows_the_next_page_of_how_many()
    {
        var app = new AppDriver(nuGetRequests: _fake);
        _fake.AnswerSearches(ThreePagesOfPackages);
        var picker = app.Ui.NuGetPicker;
        picker.UseSetup(Setup());

        picker.Open();
        app.Frames.WaitUntil(() => picker.Browser.Results.Count > 0, AppStateDriver.Patience);
        app.Run(2);
        var first = app.Frames.Drew("Page 1 of 3");
        app.Frames.ClickOn(Selector.AtPath("nuget-next")).Frame();
        app.Frames.WaitUntil(() => !picker.Browser.IsSearching, AppStateDriver.Patience);
        app.Run(2);

        Assert.True(first);
        Assert.True(app.Frames.Drew("60 packages"));
        Assert.True(app.Frames.Drew("Page 2 of 3"));
    }

    [Fact]
    public void A_package_s_icon_is_drawn_in_its_row_once_fetched_and_freed_when_the_dialog_closes()
    {
        var app = new AppDriver(nuGetRequests: _fake);
        _fake.AnswerSearches(PackageWithIcon);
        _fake.Answer(IconUrl, SquareSvg);
        var picker = app.Ui.NuGetPicker;
        picker.UseSetup(Setup());

        picker.Open();
        app.Frames.WaitUntil(() => picker.Browser.Results.Count > 0, AppStateDriver.Patience);
        var icon = picker.Browser.EnsureIcon(picker.Browser.Results[0]);
        Assert.NotNull(icon);
        app.Frames.WaitUntil(() => icon.Shown != null, AppStateDriver.Patience);
        app.Run(2);
        var drawn = app.Frames.Canvas.Images.Any(i => i.Inputs.ImageId == icon.ImageId);
        app.Frames.Key(KeyboardKey.Escape);
        app.Run(2);

        Assert.True(drawn);
        Assert.Equal(FetchNuGetIconHandler.IconPixels, icon.Shown?.Pixels.Width);
        Assert.False(app.Frames.Canvas.HasImage(icon.ImageId));
    }

    [Fact]
    public void A_package_with_no_icon_shows_the_stand_in()
    {
        var app = new AppDriver(nuGetRequests: _fake);
        var picker = app.Ui.NuGetPicker;
        picker.UseSetup(Setup());

        picker.Open();
        app.Frames.WaitUntil(() => picker.Browser.Results.Count > 0, AppStateDriver.Patience);
        app.Run(2);

        Assert.Null(picker.Browser.EnsureIcon(picker.Browser.Results[0]));
        Assert.Empty(app.Frames.Canvas.Images);
    }

    [Fact]
    public void The_details_panel_shows_what_the_search_and_the_catalog_say_of_the_selected_package()
    {
        var app = new AppDriver(width: 1200f, height: 800f, nuGetRequests: _fake);
        _fake.AnswerSearches(PackageWithDetails);
        _fake.Answer(DetailsUrl, """
            { "catalogEntry": { "licenseExpression": "MIT",
                "dependencyGroups": [ { "targetFramework": "net8.0", "dependencies": [ { "id": "Dep", "range": "[1.0.0, )" } ] } ] } }
            """);
        var picker = app.Ui.NuGetPicker;
        picker.UseSetup(Setup());

        picker.Open();
        app.Frames.WaitUntil(() => picker.Browser.Results.Count > 0, AppStateDriver.Patience);
        app.Run(2);
        app.Frames.WaitUntil(() => app.Frames.Drew("Dep"), AppStateDriver.Patience);

        Assert.True(app.Frames.Drew("Verified owner"));
        Assert.True(app.Frames.Drew("1,234,567"));
        Assert.True(app.Frames.Drew("zee"));
        Assert.True(app.Frames.Drew("net8.0"));
        Assert.True(app.Frames.Drew("≥ 1.0.0"));
    }

    private string WritePackage()
    {
        var path = _temp.Write("source.nupkg", []);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        var name = Path.GetFileName(AppStateDriver.FixturePath);
        archive.CreateEntryFromFile(AppStateDriver.FixturePath, $"lib/net10.0/{name}");
        return path;
    }
}
