using ILSight.Core;
using ILSight.Core.Resources;
using ILSight.NuGetArea;
using ILSight.ILSightAppArea;
using static ILSight.NuGetArea.FetchNuGetIconHandler;

namespace ILSight.App.Tests;

/// <summary>The "open from NuGet" dialog's use cases over a fake port: what each does with what the port
/// answers, and the tokens it asks with.</summary>
public sealed class NuGetBrowserTests
{
    private const string FeedUrl = "https://feed.test/v3/index.json";
    private const string IconUrl = "https://feed.test/fixtures/icon";

    /// <summary>Answers each port member with what the test set; keeps what was asked, the tokens it was asked
    /// with, and the problems reported.</summary>
    private sealed class FakePorts : ReadNuGetSetupHandler.IPorts, SearchNuGetFeedHandler.IPorts, DownloadNuGetPackageHandler.IPorts,
        FetchNuGetIconHandler.IPorts, ReadNuGetDetailsHandler.IPorts
    {
        public Outcome<NuGetSetup> SetupRead { get; init; } = new Outcome<NuGetSetup>.Failed(new IOException("Reading the config was not expected."));

        public NuGetSetup? DefaultSetup { get; init; }

        public Func<FeedSearch, Task<Outcome<FeedPage>>> Search { get; init; } = static _ => Task.FromResult<Outcome<FeedPage>>(new Outcome<FeedPage>.Ok(Page("Fixtures")));

        public Outcome<PackageVersionDetails> Details { get; init; } = new Outcome<PackageVersionDetails>.Failed(new IOException("Reading details was not expected."));

        public Outcome<ResourceImage> Icon { get; init; } = new Outcome<ResourceImage>.Failed(new IOException("Downloading an icon was not expected."));

        /// <summary>Answers an icon by its URL, in place of <see cref="Icon"/>, when set.</summary>
        public Func<WebUrl, Task<Outcome<ResourceImage>>>? IconAt { get; init; }

        public Outcome<string> Package { get; init; } = new Outcome<string>.Failed(new IOException("Downloading a package was not expected."));

        public List<string> Asked { get; } = [];

        public List<FeedSearch> Searched { get; } = [];

        public List<CancellationToken> Tokens { get; } = [];

        public List<AppProblem> Reported { get; } = [];

        public void ReportError(AppProblem problem)
        {
            Reported.Add(problem);
        }

        public Task<Outcome<NuGetSetup>> ReadUserSetupAsync(CancellationToken ct)
        {
            return AnswerAsync(nameof(ReadUserSetupAsync), SetupRead, ct);
        }

        public NuGetSetup CreateDefaultSetup()
        {
            Asked.Add(nameof(CreateDefaultSetup));
            Assert.NotNull(DefaultSetup);
            return DefaultSetup;
        }

        public Task<Outcome<FeedPage>> SearchAsync(FeedSearch search, CancellationToken ct)
        {
            Asked.Add(nameof(SearchAsync));
            Searched.Add(search);
            Tokens.Add(ct);
            return Search(search);
        }

        public Task<Outcome<PackageVersionDetails>> ReadDetailsAsync(NuGetSource feed, PackageId id, FeedVersion version, CancellationToken ct)
        {
            return AnswerAsync(nameof(ReadDetailsAsync), Details, ct);
        }

        public Task<Outcome<ResourceImage>> DownloadIconAsync(WebUrl url, int side, CancellationToken ct)
        {
            if (IconAt is null)
            {
                return AnswerAsync(nameof(DownloadIconAsync), Icon, ct);
            }

            Asked.Add(nameof(DownloadIconAsync));
            Tokens.Add(ct);
            return IconAt(url);
        }

        public Task<Outcome<string>> DownloadPackageAsync(PackageCache cache, NuGetSource feed, PackageId id, PackageVersion version, CancellationToken ct)
        {
            return AnswerAsync(nameof(DownloadPackageAsync), Package, ct);
        }

        private Task<T> AnswerAsync<T>(string member, T answer, CancellationToken ct)
        {
            Asked.Add(member);
            Tokens.Add(ct);
            return Task.FromResult(answer);
        }
    }

    private static NuGetSource Source()
    {
        var source = NuGetSource.FromConfig("test", FeedUrl);
        Assert.NotNull(source);
        return source;
    }

    private static NuGetSetup Setup()
    {
        var cache = new PackageCache { GlobalPackagesFolder = Path.GetTempPath(), DownloadFolder = Path.GetTempPath() };
        return new NuGetSetup { Sources = [Source()], Cache = cache };
    }

    private static PackageVersion Version(string text)
    {
        var version = PackageVersion.Parse(text);
        Assert.NotNull(version);
        return version;
    }

    private static FeedPackage Package(string id, string? iconUrl = null)
    {
        var parsed = PackageId.Parse(id);
        Assert.NotNull(parsed);
        var version = Version("2.0.0");
        return new FeedPackage
        {
            Id = parsed,
            Version = version,
            Description = "The fixtures.",
            Authors = "Zee",
            IconUrl = WebUrl.Parse(iconUrl),
            Versions = [new FeedVersion
            {
                Version = version,
                Downloads = 10,
                DetailsUrl = null,
            }],
        };
    }

    private static FeedPage Page(string id, string? iconUrl = null)
    {
        return new FeedPage { Packages = [Package(id, iconUrl)], TotalHits = 1 };
    }

    private static ResourceImage Pixels()
    {
        return new ResourceImage { Width = IconPixels, Height = IconPixels, Rgba = new byte[IconPixels * IconPixels * 4], Format = "PNG" };
    }

    private static Task<Outcome<FeedPage>> AnswerPageAsync(FeedPage page)
    {
        return Task.FromResult<Outcome<FeedPage>>(new Outcome<FeedPage>.Ok(page));
    }

    /// <summary>A picker whose setup is read, its feed filled in, opened, and what it found told so, as each frame
    /// does.</summary>
    private static NuGetPicker CreatePicker()
    {
        var picker = new NuGetPicker { Browser = new NuGetBrowser() };
        picker.UseSetup(Setup());
        picker.Open();
        picker.Browser.StopUnlessOpen(picker.IsOpen);
        return picker;
    }

    /// <summary>The search the picker asks for now, which must be one.</summary>
    private static FeedSearch AskSearch(NuGetPicker picker, double now)
    {
        var search = picker.Update(now);
        Assert.NotNull(search);
        return search;
    }

    /// <summary>Starts the search the picker asks for at <paramref name="now"/> and runs it over
    /// <paramref name="ports"/> as the edge does, on the token of the work it was started as, which a newer search or
    /// closing the dialog cancels.</summary>
    private static Task SearchAsync(FakePorts ports, NuGetPicker picker, double now)
    {
        var search = picker.Browser.StartSearch(AskSearch(picker, now));
        return new SearchNuGetFeedHandler(ports).HandleAsync(picker.Browser, search, search.Work.Token);
    }

    /// <summary>Closes the dialog as its close box does, then tells what it found, as the frame does; returns the
    /// images it hands back to free.</summary>
    private static IReadOnlyList<string> Close(NuGetPicker picker)
    {
        picker.IsOpen = false;
        return picker.Browser.StopUnlessOpen(picker.IsOpen);
    }

    /// <summary>Starts what the dialog asked to fetch as the frame edge does, a workflow for each, and returns the
    /// errors reported once they all ended.</summary>
    private static async Task<ErrorLog> RunFetchesAsync(FakePorts ports, NuGetBrowser browser)
    {
        var work = new BackgroundWork();
        var errors = new ErrorLog();
        AppFrames.StartFetching(new WorkflowRunner(work, errors), new FetchNuGetIconHandler(ports), new ReadNuGetDetailsHandler(ports), browser);
        await WorkflowRunnerTests.WaitUntilEndedAsync(work);
        return errors;
    }

    [Fact]
    public async Task SearchAsync_PageFound_ShowsItSelectsItsFirstPackageAndKeepsTheFeed()
    {
        var ports = new FakePorts();
        var picker = CreatePicker();
        using var shutdown = new CancellationTokenSource();
        var search = AskSearch(picker, now: 10);

        await new SearchNuGetFeedHandler(ports).HandleAsync(picker.Browser, picker.Browser.StartSearch(search), shutdown.Token);

        Assert.Same(search, Assert.Single(ports.Searched));
        Assert.Equal("Fixtures", Assert.Single(picker.Browser.Results).Id.Text);
        Assert.Equal("Fixtures", picker.Browser.Selected?.Id.Text);
        Assert.False(picker.Browser.IsSearching);
        Assert.Equal(new SavedNuGetSearch { FeedUrl = FeedUrl, IncludePrerelease = false }, picker.Browser.KeptSearch);
        Assert.Null(picker.Update(now: 11));
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task SearchAsync_TokenFired_StopsTheSearchTheWorkflowWasGiven()
    {
        var answer = new TaskCompletionSource<Outcome<FeedPage>>();
        var ports = new FakePorts { Search = _ => answer.Task };
        var picker = CreatePicker();
        using var shutdown = new CancellationTokenSource();
        var searching = new SearchNuGetFeedHandler(ports).HandleAsync(picker.Browser, picker.Browser.StartSearch(AskSearch(picker, now: 10)), shutdown.Token);
        var token = Assert.Single(ports.Tokens);
        var stoppedBefore = token.IsCancellationRequested;

        await shutdown.CancelAsync();
        answer.SetCanceled(token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => searching);
        Assert.False(stoppedBefore);
        Assert.True(token.IsCancellationRequested);
        Assert.Empty(picker.Browser.Results);
        Assert.Null(picker.Browser.SearchError);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task SearchAsync_SearchFailed_SaysWhyAndReportsIt()
    {
        var ports = new FakePorts { Search = static _ => Task.FromResult<Outcome<FeedPage>>(new Outcome<FeedPage>.Failed(new IOException("The feed is down."))) };
        var picker = CreatePicker();

        await SearchAsync(ports, picker, now: 10);

        Assert.Equal("The feed is down.", picker.Browser.SearchError);
        Assert.Empty(picker.Browser.Results);
        var reported = Assert.IsType<AppProblem.NuGetSearchFailed>(Assert.Single(ports.Reported));
        Assert.Equal("test", reported.Feed);
    }

    [Fact]
    public async Task SearchAsync_NewerSearchStarted_DropsWhatTheOlderOneFinds()
    {
        var older = new TaskCompletionSource<Outcome<FeedPage>>();
        var newer = new TaskCompletionSource<Outcome<FeedPage>>();
        var answers = new Queue<Task<Outcome<FeedPage>>>([older.Task, newer.Task]);
        var ports = new FakePorts { Search = _ => answers.Dequeue() };
        var picker = CreatePicker();
        var olderSearch = SearchAsync(ports, picker, now: 10);
        picker.IncludePrerelease = true;
        var newerSearch = SearchAsync(ports, picker, now: 10);

        newer.SetResult(new Outcome<FeedPage>.Ok(Page("Newer")));
        await newerSearch;
        older.SetResult(new Outcome<FeedPage>.Ok(Page("Older")));
        await olderSearch;

        Assert.True(ports.Tokens[0].IsCancellationRequested);
        Assert.False(ports.Tokens[1].IsCancellationRequested);
        Assert.Equal("Newer", Assert.Single(picker.Browser.Results).Id.Text);
        Assert.True(picker.Browser.ShowsWhatWasAsked);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task SearchAsync_DialogClosedMeanwhile_DropsWhatItFindsAndReportsNothing()
    {
        var answer = new TaskCompletionSource<Outcome<FeedPage>>();
        var ports = new FakePorts { Search = _ => answer.Task };
        var picker = CreatePicker();
        var searching = SearchAsync(ports, picker, now: 10);

        Close(picker);
        var token = Assert.Single(ports.Tokens);
        answer.SetCanceled(token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => searching);

        Assert.True(token.IsCancellationRequested);
        Assert.False(picker.Browser.IsSearching);
        Assert.Null(picker.Browser.SearchError);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public void Update_QueryTyped_AsksForNoSearchUntilItSettles()
    {
        var ports = new FakePorts();
        var picker = CreatePicker();
        var searching = SearchAsync(ports, picker, now: 10);

        picker.Query = "fix";
        var whileTyping = picker.Update(now: 20);
        var settling = picker.Browser.IsSettling;
        var wait = picker.Browser.CalculateSecondsToSettle(20);
        var settled = picker.Update(now: 21);

        Assert.True(searching.IsCompletedSuccessfully);
        Assert.Null(whileTyping);
        Assert.True(settling);
        Assert.True(wait > 0);
        Assert.Equal("fix", settled?.Query);
        Assert.Single(ports.Searched);
    }

    [Fact]
    public async Task ReadSetupAsync_ConfigUnreadable_ReportsItAndSearchesTheDefaultSetupsFirstFeed()
    {
        var ports = new FakePorts { SetupRead = new Outcome<NuGetSetup>.Failed(new IOException("Unreadable.")), DefaultSetup = Setup() };
        var picker = new NuGetPicker { Browser = new NuGetBrowser() };
        picker.Open();
        using var shutdown = new CancellationTokenSource();

        await new ReadNuGetSetupHandler(ports).HandleAsync(picker, shutdown.Token);
        await new ReadNuGetSetupHandler(ports).HandleAsync(picker, shutdown.Token);

        Assert.Equal([nameof(FakePorts.ReadUserSetupAsync), nameof(FakePorts.CreateDefaultSetup)], ports.Asked);
        Assert.Equal(shutdown.Token, Assert.Single(ports.Tokens));
        Assert.IsType<AppProblem.NuGetConfigUnreadable>(Assert.Single(ports.Reported));
        Assert.False(picker.Browser.IsReadingSetup);
        Assert.Equal(FeedUrl, picker.FeedUrl);
    }

    [Fact]
    public async Task StartFetching_DetailsRead_TakeThePlaceOfTheLoading()
    {
        var read = new PackageVersionDetails
        {
            Published = null,
            License = "MIT",
            LicenseUrl = null,
            ProjectUrl = null,
            PackageSize = 0,
            DependencyGroups = [],
            Deprecation = null,
            Vulnerabilities = [],
        };
        var ports = new FakePorts { Details = new Outcome<PackageVersionDetails>.Ok(read) };
        var picker = CreatePicker();
        await SearchAsync(ports, picker, now: 10);
        var package = Assert.Single(picker.Browser.Results);

        var loading = picker.Browser.EnsureDetails(package, Version("2.0.0"));
        var asked = picker.Browser.HasDownloadsToStart;
        await RunFetchesAsync(ports, picker.Browser);
        var details = picker.Browser.EnsureDetails(package, Version("2.0.0"));

        Assert.IsType<VersionDetails.Loading>(loading);
        Assert.True(asked);
        Assert.False(picker.Browser.HasDownloadsToStart);
        Assert.Same(read, Assert.IsType<VersionDetails.Read>(details).Details);
        Assert.True(ports.Tokens[^1].CanBeCanceled);
    }

    [Fact]
    public async Task StartFetching_DetailsFailed_SayWhyUntilRetried()
    {
        var ports = new FakePorts { Details = new Outcome<PackageVersionDetails>.Failed(new IOException("No catalog.")) };
        var picker = CreatePicker();
        await SearchAsync(ports, picker, now: 10);
        var package = Assert.Single(picker.Browser.Results);

        Assert.IsType<VersionDetails.Loading>(picker.Browser.EnsureDetails(package, Version("2.0.0")));
        await RunFetchesAsync(ports, picker.Browser);
        var failed = picker.Browser.EnsureDetails(package, Version("2.0.0"));
        picker.Browser.RetryDetails(package, Version("2.0.0"));
        var retried = picker.Browser.EnsureDetails(package, Version("2.0.0"));

        Assert.Equal("No catalog.", Assert.IsType<VersionDetails.Failed>(failed).Error);
        Assert.IsType<VersionDetails.Loading>(retried);
        Assert.True(picker.Browser.HasDownloadsToStart);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task StartFetching_IconDownloaded_ShowsItsPixels()
    {
        var pixels = Pixels();
        var ports = new FakePorts { Search = static _ => AnswerPageAsync(Page("Fixtures", IconUrl)), Icon = new Outcome<ResourceImage>.Ok(pixels) };
        var picker = CreatePicker();
        await SearchAsync(ports, picker, now: 10);

        var icon = picker.Browser.EnsureIcon(Assert.Single(picker.Browser.Results));
        Assert.NotNull(icon);
        var shownBefore = icon.Shown;
        await RunFetchesAsync(ports, picker.Browser);

        Assert.Null(shownBefore);
        Assert.Same(pixels, icon.Shown?.Pixels);
        Assert.Equal(icon.ImageId, icon.Shown?.ImageId);
        Assert.Same(icon, picker.Browser.EnsureIcon(Assert.Single(picker.Browser.Results)));
        Assert.Single(ports.Asked, nameof(FakePorts.DownloadIconAsync));
    }

    [Fact]
    public async Task StartFetching_IconThatCannotBeDownloaded_KeepsNoPixels()
    {
        var ports = new FakePorts { Search = static _ => AnswerPageAsync(Page("Fixtures", IconUrl)) };
        var picker = CreatePicker();
        await SearchAsync(ports, picker, now: 10);
        var icon = picker.Browser.EnsureIcon(Assert.Single(picker.Browser.Results));
        Assert.NotNull(icon);

        var errors = await RunFetchesAsync(ports, picker.Browser);

        Assert.Null(icon.Shown);
        Assert.Empty(ports.Reported);
        Assert.Empty(errors.Entries);
    }

    [Fact]
    public async Task StartFetching_OneIconFailsUnexpectedly_TheOthersAreShownAndEachFailureIsReportedOnce()
    {
        var pixels = Pixels();
        var urls = new[] { "https://feed.test/a/icon", "https://feed.test/b/icon", "https://feed.test/c/icon" };
        var page = new FeedPage
        {
            Packages = [Package("A", urls[0]), Package("B", urls[1]), Package("C", urls[2])],
            TotalHits = 3,
        };
        var ports = new FakePorts
        {
            Search = _ => AnswerPageAsync(page),
            IconAt = url => url.Text == urls[2]
                ? Task.FromResult<Outcome<ResourceImage>>(new Outcome<ResourceImage>.Ok(pixels))
                : Task.FromException<Outcome<ResourceImage>>(new InvalidOperationException($"The icon at {url.Text} broke.")),
        };
        var picker = CreatePicker();
        await SearchAsync(ports, picker, now: 10);
        var icons = picker.Browser.Results
            .Select(picker.Browser.EnsureIcon)
            .ToList();

        var errors = await RunFetchesAsync(ports, picker.Browser);

        Assert.Equal(3, ports.Asked.Count(a => a == nameof(FakePorts.DownloadIconAsync)));
        var first = icons[0];
        var second = icons[1];
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(first.Shown);
        Assert.Null(second.Shown);
        Assert.Same(pixels, icons[2]?.Shown?.Pixels);
        Assert.Equal(2, errors.Entries.Count);
        Assert.All(errors.Entries, static e => Assert.IsType<AppProblem.Unexpected>(e.Problem));
        Assert.False(picker.Browser.HasDownloadsToStart);
    }

    [Fact]
    public async Task StartFetching_DialogClosedBeforeItRuns_DownloadsNothing()
    {
        var ports = new FakePorts { Search = static _ => AnswerPageAsync(Page("Fixtures", IconUrl)) };
        var picker = CreatePicker();
        await SearchAsync(ports, picker, now: 10);
        picker.Browser.EnsureIcon(Assert.Single(picker.Browser.Results));

        var images = Close(picker);
        await RunFetchesAsync(ports, picker.Browser);

        Assert.Single(images);
        Assert.DoesNotContain(nameof(FakePorts.DownloadIconAsync), ports.Asked);
    }

    [Fact]
    public async Task StopUnlessOpen_WhileOpenOrOnceStopped_HandsBackNoImagesAndAllocatesNothing()
    {
        var ports = new FakePorts { Search = static _ => AnswerPageAsync(Page("Fixtures", IconUrl)) };
        var picker = CreatePicker();
        await SearchAsync(ports, picker, now: 10);
        picker.Browser.EnsureIcon(Assert.Single(picker.Browser.Results));
        picker.Browser.StopUnlessOpen(true);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var whileOpen = picker.Browser.StopUnlessOpen(true).Count;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var closing = Close(picker);
        var closedAgain = picker.Browser.StopUnlessOpen(false);

        Assert.Equal(0, whileOpen);
        Assert.Equal(0, allocated);
        Assert.Single(closing);
        Assert.Empty(closedAgain);
    }

    [Fact]
    public async Task DownloadAsync_Downloaded_HandsTheFileOverOnce()
    {
        var ports = new FakePorts { Package = new Outcome<string>.Ok("fixtures.2.0.0.nupkg") };
        var picker = CreatePicker();
        await SearchAsync(ports, picker, now: 10);
        picker.Browser.FollowSelection();
        using var shutdown = new CancellationTokenSource();

        var download = picker.Browser.StartDownload();
        Assert.NotNull(download);
        await new DownloadNuGetPackageHandler(ports).HandleAsync(picker.Browser, download, shutdown.Token);

        Assert.Equal("fixtures.2.0.0.nupkg", picker.Browser.TakeDownloaded());
        Assert.Null(picker.Browser.TakeDownloaded());
        Assert.False(picker.Browser.IsDownloading);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task Submit_PackagePicked_SaysItIsDownloadingBeforeTheDownloadRuns()
    {
        var ports = new FakePorts { Package = new Outcome<string>.Ok("fixtures.2.0.0.nupkg") };
        var picker = CreatePicker();
        await SearchAsync(ports, picker, now: 10);
        picker.Browser.FollowSelection();

        var download = picker.Browser.Submit();
        var downloading = picker.Browser.IsDownloading;
        var name = picker.Browser.DownloadingName;
        var canOpenAgain = picker.Browser.CanOpen;
        var askedBeforeTheEdgeRan = ports.Asked.Contains(nameof(FakePorts.DownloadPackageAsync));
        Assert.NotNull(download);
        await new DownloadNuGetPackageHandler(ports).HandleAsync(picker.Browser, download, download.Work.Token);

        Assert.True(downloading);
        Assert.Equal("Fixtures 2.0.0", name);
        Assert.False(canOpenAgain);
        Assert.False(askedBeforeTheEdgeRan);
        Assert.Equal("fixtures.2.0.0.nupkg", picker.Browser.TakeDownloaded());
    }

    [Fact]
    public async Task Submit_WhileTheQuerySettles_SearchesItInsteadOfDownloading()
    {
        var ports = new FakePorts { Package = new Outcome<string>.Ok("fixtures.2.0.0.nupkg") };
        var picker = CreatePicker();
        await SearchAsync(ports, picker, now: 10);
        picker.Browser.FollowSelection();

        picker.Query = "fix";
        picker.Update(now: 20);
        var download = picker.Browser.Submit();
        var search = picker.Update(now: 20.01);

        Assert.Null(download);
        Assert.Equal("fix", search?.Query);
        Assert.DoesNotContain(nameof(FakePorts.DownloadPackageAsync), ports.Asked);
        Assert.Null(picker.Browser.TakeDownloaded());
    }
}
