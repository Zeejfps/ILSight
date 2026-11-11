using ILSight.Core;
using ILSight.Core.Resources;
using static ILSight.NuGetBrowser;

namespace ILSight.App.Tests;

/// <summary>The "open from NuGet" dialog's workflows over a fake port: what each does with what the port
/// answers, and the tokens it asks with.</summary>
public sealed class NuGetBrowserTests
{
    private const string FeedUrl = "https://feed.test/v3/index.json";
    private const string IconUrl = "https://feed.test/fixtures/icon";

    /// <summary>Answers each port member with what the test set; keeps what was asked, the tokens it was asked
    /// with, and the problems reported.</summary>
    private sealed class FakePorts : IPorts
    {
        public Outcome<NuGetSetup> SetupRead { get; init; } = new Outcome<NuGetSetup>.Failed(new IOException("Reading the config was not expected."));

        public NuGetSetup? DefaultSetup { get; init; }

        public Func<FeedSearch, Task<Outcome<FeedPage>>> Search { get; init; } = static _ => Task.FromResult<Outcome<FeedPage>>(new Outcome<FeedPage>.Ok(Page("Fixtures")));

        public Outcome<PackageVersionDetails> Details { get; init; } = new Outcome<PackageVersionDetails>.Failed(new IOException("Reading details was not expected."));

        public Outcome<ResourceImage> Icon { get; init; } = new Outcome<ResourceImage>.Failed(new IOException("Downloading an icon was not expected."));

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
            return AnswerAsync(nameof(DownloadIconAsync), Icon, ct);
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
        return new FeedPackage(parsed, version, "The fixtures.", "Zee", WebUrl.Parse(iconUrl), [new FeedVersion(version, 10, null)]);
    }

    private static FeedPage Page(string id, string? iconUrl = null)
    {
        return new FeedPage([Package(id, iconUrl)], 1);
    }

    private static Task<Outcome<FeedPage>> AnswerPageAsync(FeedPage page)
    {
        return Task.FromResult<Outcome<FeedPage>>(new Outcome<FeedPage>.Ok(page));
    }

    /// <summary>A picker over <paramref name="ports"/> whose setup is read, its feed filled in.</summary>
    private static NuGetPicker CreatePicker(FakePorts ports)
    {
        var picker = new NuGetPicker { Browser = new NuGetBrowser(ports) };
        NuGetPicking.UseSetup(picker, Setup());
        NuGetPicking.OpenPicker(picker);
        return picker;
    }

    /// <summary>The search the picker asks for now, which must be one.</summary>
    private static FeedSearch AskSearch(NuGetPicker picker, double now)
    {
        var search = NuGetPicking.UpdatePicker(picker, now);
        Assert.NotNull(search);
        return search;
    }

    [Fact]
    public async Task SearchAsync_PageFound_ShowsItSelectsItsFirstPackageAndKeepsTheFeed()
    {
        var ports = new FakePorts();
        var picker = CreatePicker(ports);
        using var shutdown = new CancellationTokenSource();
        var search = AskSearch(picker, now: 10);

        await picker.Browser.SearchAsync(search, shutdown.Token);

        Assert.Same(search, Assert.Single(ports.Searched));
        Assert.Equal("Fixtures", Assert.Single(picker.Browser.Results).Id.Text);
        Assert.Equal("Fixtures", picker.Browser.Selected?.Id.Text);
        Assert.False(picker.Browser.IsSearching);
        Assert.Equal(new SavedNuGetSearch(FeedUrl, false), picker.Browser.KeptSearch);
        Assert.Null(NuGetPicking.UpdatePicker(picker, now: 11));
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task SearchAsync_TokenFired_StopsTheSearchTheWorkflowWasGiven()
    {
        var answer = new TaskCompletionSource<Outcome<FeedPage>>();
        var ports = new FakePorts { Search = _ => answer.Task };
        var picker = CreatePicker(ports);
        using var shutdown = new CancellationTokenSource();
        var searching = picker.Browser.SearchAsync(AskSearch(picker, now: 10), shutdown.Token);
        var token = Assert.Single(ports.Tokens);
        var stoppedBefore = token.IsCancellationRequested;

        await shutdown.CancelAsync();
        answer.SetResult(new Outcome<FeedPage>.Failed(new OperationCanceledException(token)));

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
        var picker = CreatePicker(ports);

        await picker.Browser.SearchAsync(AskSearch(picker, now: 10), CancellationToken.None);

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
        var picker = CreatePicker(ports);
        var olderSearch = picker.Browser.SearchAsync(AskSearch(picker, now: 10), CancellationToken.None);
        picker.IncludePrerelease = true;
        var newerSearch = picker.Browser.SearchAsync(AskSearch(picker, now: 10), CancellationToken.None);

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
        var picker = CreatePicker(ports);
        var searching = picker.Browser.SearchAsync(AskSearch(picker, now: 10), CancellationToken.None);

        NuGetPicking.ClosePicker(picker);
        answer.SetResult(new Outcome<FeedPage>.Failed(new OperationCanceledException()));
        await searching;

        Assert.True(Assert.Single(ports.Tokens).IsCancellationRequested);
        Assert.False(picker.Browser.IsSearching);
        Assert.Null(picker.Browser.SearchError);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public void Update_QueryTyped_AsksForNoSearchUntilItSettles()
    {
        var ports = new FakePorts();
        var picker = CreatePicker(ports);
        var first = AskSearch(picker, now: 10);
        var searching = picker.Browser.SearchAsync(first, CancellationToken.None);

        picker.Query = "fix";
        var whileTyping = NuGetPicking.UpdatePicker(picker, now: 20);
        var settling = picker.Browser.IsSettling;
        var wait = picker.Browser.CalculateSecondsToSettle(20);
        var settled = NuGetPicking.UpdatePicker(picker, now: 21);

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
        var picker = new NuGetPicker { Browser = new NuGetBrowser(ports) };
        NuGetPicking.OpenPicker(picker);
        using var shutdown = new CancellationTokenSource();

        await NuGetPicking.ReadSetupAsync(picker, shutdown.Token);
        await NuGetPicking.ReadSetupAsync(picker, shutdown.Token);

        Assert.Equal([nameof(IPorts.ReadUserSetupAsync), nameof(IPorts.CreateDefaultSetup)], ports.Asked);
        Assert.Equal(shutdown.Token, Assert.Single(ports.Tokens));
        Assert.IsType<AppProblem.NuGetConfigUnreadable>(Assert.Single(ports.Reported));
        Assert.False(picker.Browser.IsReadingSetup);
        Assert.Equal(FeedUrl, picker.FeedUrl);
    }

    [Fact]
    public async Task DownloadAskedAsync_DetailsRead_TakeThePlaceOfTheLoading()
    {
        var read = new PackageVersionDetails(null, "MIT", null, null, 0, [], null, []);
        var ports = new FakePorts { Details = new Outcome<PackageVersionDetails>.Ok(read) };
        var picker = CreatePicker(ports);
        await picker.Browser.SearchAsync(AskSearch(picker, now: 10), CancellationToken.None);
        var package = Assert.Single(picker.Browser.Results);
        using var shutdown = new CancellationTokenSource();

        var loading = picker.Browser.EnsureDetails(package, Version("2.0.0"));
        var asked = picker.Browser.HasDownloadsToStart;
        await picker.Browser.DownloadAskedAsync(shutdown.Token);
        var details = picker.Browser.EnsureDetails(package, Version("2.0.0"));

        Assert.IsType<VersionDetails.Loading>(loading);
        Assert.True(asked);
        Assert.False(picker.Browser.HasDownloadsToStart);
        Assert.Same(read, Assert.IsType<VersionDetails.Read>(details).Details);
        Assert.True(ports.Tokens[^1].CanBeCanceled);
    }

    [Fact]
    public async Task DownloadAskedAsync_DetailsFailed_SayWhyUntilRetried()
    {
        var ports = new FakePorts { Details = new Outcome<PackageVersionDetails>.Failed(new IOException("No catalog.")) };
        var picker = CreatePicker(ports);
        await picker.Browser.SearchAsync(AskSearch(picker, now: 10), CancellationToken.None);
        var package = Assert.Single(picker.Browser.Results);

        picker.Browser.EnsureDetails(package, Version("2.0.0"));
        await picker.Browser.DownloadAskedAsync(CancellationToken.None);
        var failed = picker.Browser.EnsureDetails(package, Version("2.0.0"));
        picker.Browser.RetryDetails(package, Version("2.0.0"));
        var retried = picker.Browser.EnsureDetails(package, Version("2.0.0"));

        Assert.Equal("No catalog.", Assert.IsType<VersionDetails.Failed>(failed).Error);
        Assert.IsType<VersionDetails.Loading>(retried);
        Assert.True(picker.Browser.HasDownloadsToStart);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task DownloadAskedAsync_IconDownloaded_ShowsItsPixels()
    {
        var pixels = new ResourceImage { Width = IconPixels, Height = IconPixels, Rgba = new byte[IconPixels * IconPixels * 4], Format = "PNG" };
        var ports = new FakePorts { Search = static _ => AnswerPageAsync(Page("Fixtures", IconUrl)), Icon = new Outcome<ResourceImage>.Ok(pixels) };
        var picker = CreatePicker(ports);
        await picker.Browser.SearchAsync(AskSearch(picker, now: 10), CancellationToken.None);

        var icon = picker.Browser.EnsureIcon(Assert.Single(picker.Browser.Results));
        Assert.NotNull(icon);
        var shownBefore = icon.Shown;
        await picker.Browser.DownloadAskedAsync(CancellationToken.None);

        Assert.Null(shownBefore);
        Assert.Same(pixels, icon.Shown?.Pixels);
        Assert.Equal(icon.ImageId, icon.Shown?.ImageId);
        Assert.Same(icon, picker.Browser.EnsureIcon(Assert.Single(picker.Browser.Results)));
        Assert.Single(ports.Asked, nameof(IPorts.DownloadIconAsync));
    }

    [Fact]
    public async Task DownloadAskedAsync_IconThatCannotBeDownloaded_KeepsNoPixels()
    {
        var ports = new FakePorts { Search = static _ => AnswerPageAsync(Page("Fixtures", IconUrl)) };
        var picker = CreatePicker(ports);
        await picker.Browser.SearchAsync(AskSearch(picker, now: 10), CancellationToken.None);
        var icon = picker.Browser.EnsureIcon(Assert.Single(picker.Browser.Results));
        Assert.NotNull(icon);

        await picker.Browser.DownloadAskedAsync(CancellationToken.None);

        Assert.Null(icon.Shown);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task DownloadAskedAsync_DialogClosedBeforeItRuns_DownloadsNothing()
    {
        var ports = new FakePorts { Search = static _ => AnswerPageAsync(Page("Fixtures", IconUrl)) };
        var picker = CreatePicker(ports);
        await picker.Browser.SearchAsync(AskSearch(picker, now: 10), CancellationToken.None);
        picker.Browser.EnsureIcon(Assert.Single(picker.Browser.Results));

        var images = NuGetPicking.ClosePicker(picker);
        await picker.Browser.DownloadAskedAsync(CancellationToken.None);

        Assert.Single(images);
        Assert.DoesNotContain(nameof(IPorts.DownloadIconAsync), ports.Asked);
    }

    [Fact]
    public async Task DownloadPickedAsync_Downloaded_HandsTheFileOverOnce()
    {
        var ports = new FakePorts { Package = new Outcome<string>.Ok("fixtures.2.0.0.nupkg") };
        var picker = CreatePicker(ports);
        await picker.Browser.SearchAsync(AskSearch(picker, now: 10), CancellationToken.None);
        picker.Browser.FollowSelection();
        using var shutdown = new CancellationTokenSource();

        await picker.Browser.DownloadPickedAsync(shutdown.Token);

        Assert.Equal("fixtures.2.0.0.nupkg", picker.Browser.TakeDownloaded());
        Assert.Null(picker.Browser.TakeDownloaded());
        Assert.False(picker.Browser.IsDownloading);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task SubmitAsync_WhileTheQuerySettles_SearchesItInsteadOfDownloading()
    {
        var ports = new FakePorts { Package = new Outcome<string>.Ok("fixtures.2.0.0.nupkg") };
        var picker = CreatePicker(ports);
        await picker.Browser.SearchAsync(AskSearch(picker, now: 10), CancellationToken.None);
        picker.Browser.FollowSelection();

        picker.Query = "fix";
        NuGetPicking.UpdatePicker(picker, now: 20);
        await picker.Browser.SubmitAsync(CancellationToken.None);
        var search = NuGetPicking.UpdatePicker(picker, now: 20.01);

        Assert.Equal("fix", search?.Query);
        Assert.DoesNotContain(nameof(IPorts.DownloadPackageAsync), ports.Asked);
        Assert.Null(picker.Browser.TakeDownloaded());
    }
}
