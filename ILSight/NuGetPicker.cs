using ZGF.Gui.Immediate;
using static ILSight.NuGetPicker;
using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Resources;

namespace ILSight;

/// <summary>
/// The "open from NuGet" dialog: the feed typed or picked from the user's NuGet.Config, read the
/// first time it opens, searched a page at a time once the query settles; the packages found, with
/// their icons; the details of the version picked of the package selected; and that version, fetched
/// into the package cache for the view to open.
/// </summary>
public sealed class NuGetPicker
{
    /// <summary>How many pixels across a package's icon is fetched at, enough for its row on a screen
    /// of twice the usual density.</summary>
    public const int IconPixels = 64;

    /// <summary>Where the work it starts in the background is counted until it ends.</summary>
    public required BackgroundWork Work { get; init; }

    /// <summary>Tells the user a search or a download failed.</summary>
    public required Action<AppProblem> ReportError { get; init; }

    public readonly Dictionary<WebUrl, PackageIcon> Icons = new();
    public readonly Dictionary<DetailsKey, VersionDetails> Details = new();
    public CancellationTokenSource Fetches = new();
    public int IconCount;
    public SetupState Setup = new SetupState.Unread();
    public SearchRun Run = new SearchRun.None();
    public ShownPage? ShownResults;
    public DownloadState DownloadRun = new DownloadState.None();
    public int RequestedPage;
    public bool IsSettling;
    public string TypedQuery = "";
    public string TypedUrl = "";
    public double TypedAt;
    public SavedNuGetSearch? Kept;
    public FeedPackage? VersionsOf;
    public bool FocusQuery;

    public bool IsOpen;

    /// <summary>Where the dialog's window is on the screen.</summary>
    public DialogWindow Window { get; } = new();

    public string Query = "";

    /// <summary>The URL of the service index of the feed to search, typed or picked from <see cref="Sources"/>.</summary>
    public string FeedUrl = "";

    public bool IncludePrerelease;

    /// <summary>The version of the selected package to open; null while none is selected.</summary>
    public PackageVersion? Version;

    public ItemSelection<FeedPackage> Selection { get; } = new();

    public FeedPackage? Selected => Selection.Item;

    /// <summary>The packages on the page shown.</summary>
    public IReadOnlyList<FeedPackage> Results
    {
        get
        {
            if (ShownResults == null)
            {
                return [];
            }

            return ShownResults.Found.Packages;
        }
    }

    /// <summary>The page shown, from 0.</summary>
    public int Page
    {
        get
        {
            if (ShownResults == null)
            {
                return 0;
            }

            return ShownResults.Search.Page;
        }
    }

    /// <summary>The page asked for, from 0: the one shown, or the one loading in its place.</summary>

    /// <summary>Whether another page of the search shown is loading in its place.</summary>
    public bool IsLoadingPage => IsSearching && RequestedPage != Page;

    /// <summary>How many pages the search shown has, at least one.</summary>
    public int PageCount => NuGetFeeds.CountPages(TotalHits);

    /// <summary>How many packages the search shown matched in all.</summary>
    public int TotalHits
    {
        get
        {
            if (ShownResults == null)
            {
                return 0;
            }

            return ShownResults.Found.TotalHits;
        }
    }

    /// <summary>How many of <see cref="TotalHits"/> the pages reach.</summary>
    public int BrowsableHits => Math.Min(TotalHits, NuGetFeed.MaxSkip + NuGetFeed.PageSize);

    public bool HasPreviousPage => Page > 0;

    public bool HasNextPage => Page + 1 < PageCount;

    /// <summary>The feeds the user's NuGet.Config lists, once the setup is read.</summary>
    public IReadOnlyList<NuGetSource> Sources
    {
        get
        {
            var setup = NuGetPicking.FindReadySetup(this);
            if (setup == null)
            {
                return [];
            }

            return setup.Sources;
        }
    }

    public bool IsReadingSetup => Setup is not SetupState.Ready;

    /// <summary>Whether <see cref="FeedUrl"/> is a URL a feed could be at.</summary>
    public bool HasFeedUrl => NuGetSource.FromUrl(FeedUrl) != null;

    /// <summary>The query the packages shown were found by.</summary>
    public string ShownQuery
    {
        get
        {
            if (ShownResults == null)
            {
                return "";
            }

            return ShownResults.Search.Query;
        }
    }

    /// <summary>The versions of the selected package, newest first.</summary>
    public PackageVersion[] Versions { get; set; } = [];

    /// <summary>How <see cref="Versions"/> are written, in their order.</summary>
    public string[] VersionTexts { get; set; } = [];

    public bool IsSearching => Run is SearchRun.Running;

    /// <summary>Why the last search failed, or null.</summary>
    public string? SearchError
    {
        get
        {
            if (Run is not SearchRun.Failed failed)
            {
                return null;
            }

            return failed.Error;
        }
    }

    public bool IsDownloading => DownloadRun is DownloadState.Running;

    /// <summary>The package and version being downloaded; empty while none is.</summary>
    public string DownloadingName
    {
        get
        {
            if (DownloadRun is not DownloadState.Running running)
            {
                return "";
            }

            return running.Name;
        }
    }

    /// <summary>How many times the feed searched or whether prereleases count changed, for the
    /// settings to keep <see cref="GetKeptSearch"/> once it did.</summary>
    public int SearchChanges { get; set; }

    /// <summary>Whether the packages shown are what the feed, query and prerelease choice now ask for:
    /// not while a changed query settles or another search runs. Another page loading over them, or
    /// failing to, leaves them so.</summary>
    public bool ShowsWhatWasAsked
    {
        get
        {
            if (IsSettling)
            {
                return false;
            }

            var searched = NuGetPicking.FindLastSearched(this);
            if (searched == null)
            {
                return true;
            }

            return NuGetPicking.IsSamePackages(searched, NuGetPicking.FindShownSearch(this));
        }
    }

    /// <summary>Whether the selected package can be opened: it is one of what was asked for, a version
    /// is picked, and no download runs.</summary>
    public bool CanOpen
    {
        get
        {
            if (Selected == null)
            {
                return false;
            }

            if (IsDownloading)
            {
                return false;
            }

            if (!ShowsWhatWasAsked)
            {
                return false;
            }

            return Version != null;
        }
    }

    /// <summary>How many times <see cref="Version"/> of the selected package was downloaded; 0 when the feed does not say.</summary>
    public long VersionDownloads
    {
        get
        {
            if (Selected == null || Version == null)
            {
                return 0;
            }

            var listed = NuGetPicking.FindVersion(Selected, Version);
            if (listed == null)
            {
                return 0;
            }

            return listed.Downloads;
        }
    }

    /// <summary>What a search asked of which feed.</summary>
    public sealed record FeedSearch(NuGetFeed Feed, string Query, bool IncludePrerelease, int Page);

    /// <summary>The page of packages shown, and the search that found it.</summary>
    public sealed record ShownPage(FeedSearch Search, FeedPage Found);

    /// <summary>Whether the user's NuGet.Config is read yet.</summary>
    public abstract record SetupState
    {
        private SetupState()
        {
        }

        public sealed record Unread : SetupState;

        public sealed record Reading : SetupState;

        public sealed record Ready(NuGetSetup Setup) : SetupState;
    }

    /// <summary>The last search: none since the dialog was made or the search was dropped, one
    /// running, or one that found a page or failed to.</summary>
    public abstract record SearchRun
    {
        private SearchRun()
        {
        }

        public sealed record None : SearchRun;

        public sealed record Running(FeedSearch Search, CancellationTokenSource Cancel) : SearchRun;

        public sealed record Found(FeedSearch Search) : SearchRun;

        public sealed record Failed(FeedSearch Search, string Error) : SearchRun;
    }

    /// <summary>A download: none, one running, or the package file one fetched, until it is taken.</summary>
    public abstract record DownloadState
    {
        private DownloadState()
        {
        }

        public sealed record None : DownloadState;

        public sealed record Running(string Name, CancellationTokenSource Cancel) : DownloadState;

        public sealed record Done(string Path) : DownloadState;
    }

    /// <summary>Which version of which package of which feed details are of.</summary>
    public readonly record struct DetailsKey(WebUrl Feed, PackageId Id, PackageVersion Version);
}

/// <summary>What the "open from NuGet" dialog does: searching a feed as the user types, a page at a time, fetching icons and version details, and downloading the package picked.</summary>
public static class NuGetPicking
{
    private const double DebounceSeconds = 0.3;
    private const string IconImagePrefix = "nuget-icon-";

    /// <summary>Searches the feed and counts prereleases as the settings kept them.</summary>
    public static void RestoreSearch(NuGetPicker picker, SavedNuGetSearch? saved)
    {
        if (saved == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(saved.FeedUrl))
        {
            return;
        }

        picker.Kept = saved;
        picker.FeedUrl = saved.FeedUrl;
        picker.IncludePrerelease = saved.IncludePrerelease;
    }

    /// <summary>The feed last searched and whether prereleases counted, for the settings to keep.</summary>
    public static SavedNuGetSearch? GetKeptSearch(NuGetPicker picker)
    {
        return picker.Kept;
    }

    public static void OpenPicker(NuGetPicker picker)
    {
        picker.IsOpen = true;
        picker.FocusQuery = true;
        if (picker.Setup is SetupState.Unread)
        {
            ReadSetup(picker);
        }
    }

    /// <summary>Reads the user's NuGet.Config in the background; one that cannot be read at all is
    /// reported, and nuget.org searched in its place.</summary>
    private static void ReadSetup(NuGetPicker picker)
    {
        Background.Start(picker.Work, nameof(ReadSetupAsync), ReadSetupAsync(picker), picker.ReportError);
    }

    private static async Task ReadSetupAsync(NuGetPicker picker)
    {
        picker.Setup = new SetupState.Reading();
        var read = await Background.Run(NuGetFeeds.ReadUserSetup);
        switch (read)
        {
            case Outcome<NuGetSetup>.Ok ok:
                UseSetup(picker, ok.Value);
                break;
            case Outcome<NuGetSetup>.Failed failed:
                picker.ReportError(new AppProblem.NuGetConfigUnreadable(failed.Error));
                UseSetup(picker, NuGetFeeds.CreateDefaultSetup());
                break;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Searches and downloads with <paramref name="setup"/> from now on, in place of the one
    /// read from the user's NuGet.Config; with no feed kept, its first feed is the one searched.</summary>
    internal static void UseSetup(NuGetPicker picker, NuGetSetup setup)
    {
        picker.Setup = new SetupState.Ready(setup);
        if (picker.FeedUrl.Length > 0)
        {
            return;
        }

        if (setup.Sources.Count == 0)
        {
            return;
        }

        picker.FeedUrl = setup.Sources[0].Url.Text;
    }

    /// <summary>Searches <paramref name="source"/> at once.</summary>
    public static void PickFeed(NuGetPicker picker, NuGetSource source)
    {
        picker.FeedUrl = source.Url.Text;
        picker.TypedUrl = source.Url.Text;
        picker.TypedAt = double.NegativeInfinity;
    }

    /// <summary>Closes the dialog, stopping the search, the download and the fetches it started.
    /// Returns the images of the icons shown, for the screen to free.</summary>
    public static List<string> ClosePicker(NuGetPicker picker)
    {
        picker.IsOpen = false;
        StopSearch(picker);
        StopDownload(picker);
        picker.Fetches.Cancel();
        picker.Fetches = new CancellationTokenSource();
        picker.Details.Clear();
        return DropIcons(picker);
    }

    internal static NuGetSetup? FindReadySetup(NuGetPicker picker)
    {
        if (picker.Setup is not SetupState.Ready ready)
        {
            return null;
        }

        return ready.Setup;
    }

    private static void StopSearch(NuGetPicker picker)
    {
        if (picker.Run is not SearchRun.Running running)
        {
            return;
        }

        running.Cancel.Cancel();
        picker.Run = new SearchRun.None();
    }

    private static void StopDownload(NuGetPicker picker)
    {
        if (picker.DownloadRun is not DownloadState.Running running)
        {
            return;
        }

        running.Cancel.Cancel();
        picker.DownloadRun = new DownloadState.None();
    }

    private static List<string> DropIcons(NuGetPicker picker)
    {
        var images = picker.Icons.Values
            .Select(i => i.ImageId)
            .ToList();
        picker.Icons.Clear();
        return images;
    }

    /// <summary>True once after <see cref="OpenPicker"/>: the query box should take focus.</summary>
    public static bool TakeQueryFocus(NuGetPicker picker)
    {
        var focus = picker.FocusQuery;
        picker.FocusQuery = false;
        return focus;
    }

    public static void ShowPreviousPage(NuGetPicker picker)
    {
        if (!picker.HasPreviousPage)
        {
            return;
        }

        picker.RequestedPage = picker.Page - 1;
    }

    public static void ShowNextPage(NuGetPicker picker)
    {
        if (!picker.HasNextPage)
        {
            return;
        }

        picker.RequestedPage = picker.Page + 1;
    }

    /// <summary>Searches again for what the last search asked, after it failed.</summary>
    public static void RetrySearch(NuGetPicker picker)
    {
        StopSearch(picker);
        picker.Run = new SearchRun.None();
    }

    public static void ClearQuery(NuGetPicker picker)
    {
        picker.Query = "";
    }

    /// <summary>Opens the selected package at <see cref="Version"/>, unless the query typed is still to
    /// be searched: then it is searched at once instead, so Enter never opens what an older query found.</summary>
    public static void SubmitSearch(NuGetPicker picker)
    {
        if (picker.IsSettling)
        {
            SearchNow(picker);
            return;
        }
        if (picker.IsSearching)
        {
            return;
        }

        DownloadPicked(picker);
    }

    /// <summary>Searches what was typed with the next update, without waiting for it to settle.</summary>
    public static void SearchNow(NuGetPicker picker)
    {
        picker.TypedAt = double.NegativeInfinity;
    }

    /// <summary>Searches again once the typed feed or query settled, or at once when another page is
    /// asked for or whether prereleases count changed; a new search starts on its first page. Returns
    /// the seconds left before what was typed settles, or 0.</summary>
    public static double UpdatePicker(NuGetPicker picker, double now)
    {
        NoteTyping(picker, now);
        picker.IsSettling = false;
        var setup = FindReadySetup(picker);
        if (setup == null)
        {
            return 0;
        }

        var source = NuGetSource.FromUrl(picker.TypedUrl);
        if (source == null)
        {
            DropResults(picker);
            return 0;
        }
        var wanted = new FeedSearch(NuGetFeeds.EnsureFeed(setup, source), picker.TypedQuery, picker.IncludePrerelease, picker.RequestedPage);
        if (IsNewSearch(picker, wanted))
        {
            picker.RequestedPage = 0;
            wanted = wanted with { Page = 0 };
        }
        if (wanted == FindLastSearched(picker))
        {
            return 0;
        }

        var remaining = DebounceSeconds - (now - picker.TypedAt);
        if (remaining > 0 && IsOnlyTypingChanged(picker, wanted))
        {
            picker.IsSettling = true;
            return remaining;
        }
        StartSearch(picker, wanted);
        return 0;
    }

    private static void NoteTyping(NuGetPicker picker, double now)
    {
        var typed = picker.Query != picker.TypedQuery || picker.FeedUrl != picker.TypedUrl;
        if (!typed)
        {
            return;
        }

        picker.TypedQuery = picker.Query;
        picker.TypedUrl = picker.FeedUrl;
        picker.TypedAt = now;
    }

    /// <summary>Clears what was found once the feed typed is no URL, so nothing shows from another feed.</summary>
    private static void DropResults(NuGetPicker picker)
    {
        StopSearch(picker);
        picker.Run = new SearchRun.None();
        ClearShown(picker);
        picker.RequestedPage = 0;
    }

    private static void ClearShown(NuGetPicker picker)
    {
        picker.ShownResults = null;
        ItemSelections.Clear(picker.Selection);
    }

    /// <summary>The search running or last run; null before any, or once it was stopped or dropped.</summary>
    internal static FeedSearch? FindLastSearched(NuGetPicker picker)
    {
        return picker.Run switch
            {
                SearchRun.None => null,
                SearchRun.Running running => running.Search,
                SearchRun.Found found => found.Search,
                SearchRun.Failed failed => failed.Search,
                _ => throw new UnreachableException(),
            };
    }

    internal static FeedSearch? FindShownSearch(NuGetPicker picker)
    {
        if (picker.ShownResults == null)
        {
            return null;
        }

        return picker.ShownResults.Search;
    }

    /// <summary>The search the next one is weighed against: the one running or last run, else the one shown.</summary>
    private static FeedSearch? FindLastAsked(NuGetPicker picker)
    {
        var searched = FindLastSearched(picker);
        if (searched != null)
        {
            return searched;
        }

        return FindShownSearch(picker);
    }

    /// <summary>Whether <paramref name="wanted"/> asks for other packages than the last search, not
    /// just another page of them.</summary>
    private static bool IsNewSearch(NuGetPicker picker, FeedSearch wanted)
    {
        var last = FindLastAsked(picker);
        if (last == null)
        {
            return false;
        }

        return !IsSamePackages(wanted, last);
    }

    internal static bool IsSamePackages(FeedSearch a, FeedSearch? b)
    {
        if (b == null)
        {
            return false;
        }

        if (a.Feed != b.Feed)
        {
            return false;
        }

        if (a.Query != b.Query)
        {
            return false;
        }

        return a.IncludePrerelease == b.IncludePrerelease;
    }

    private static bool IsOnlyTypingChanged(NuGetPicker picker, FeedSearch wanted)
    {
        var last = FindLastAsked(picker);
        if (last == null)
        {
            return false;
        }

        if (wanted.IncludePrerelease != last.IncludePrerelease)
        {
            return false;
        }

        return wanted.Page == last.Page;
    }

    private static void StartSearch(NuGetPicker picker, FeedSearch wanted)
    {
        Background.Start(picker.Work, nameof(StartSearchAsync), StartSearchAsync(picker, wanted), picker.ReportError);
    }

    private static async Task StartSearchAsync(NuGetPicker picker, FeedSearch wanted)
    {
        StopSearch(picker);
        KeepSearched(picker, wanted);
        var running = new SearchRun.Running(wanted, new CancellationTokenSource());
        picker.Run = running;
        var found = await Background.Wait(NuGetFeeds.SearchAsync(wanted.Feed, wanted.Query, wanted.IncludePrerelease, wanted.Page, running.Cancel.Token));
        if (!ReferenceEquals(picker.Run, running))
        {
            return;
        }

        picker.Run = ShowFoundPage(picker, wanted, found);
    }

    /// <summary>Has the settings keep the feed searched and whether prereleases count, once either changed.</summary>
    private static void KeepSearched(NuGetPicker picker, FeedSearch searched)
    {
        var kept = new SavedNuGetSearch(searched.Feed.Source.Url.Text, searched.IncludePrerelease);
        if (kept == picker.Kept)
        {
            return;
        }

        picker.Kept = kept;
        picker.SearchChanges++;
    }

    /// <summary>Shows the page found in place of the one shown. A failed search says why, and keeps
    /// the page shown when it only asked for another page of it.</summary>
    private static SearchRun ShowFoundPage(NuGetPicker picker, FeedSearch searched, Outcome<FeedPage> found)
    {
        switch (found)
        {
            case Outcome<FeedPage>.Ok ok:
                ShowFoundPage(picker, searched, ok.Value);
                return new SearchRun.Found(searched);
            case Outcome<FeedPage>.Failed failed:
                ReportSearchFailure(picker, searched, failed.Error);
                return new SearchRun.Failed(searched, failed.Error.Message);
            default:
                throw new UnreachableException();
        }
    }

    private static void ShowFoundPage(NuGetPicker picker, FeedSearch searched, FeedPage page)
    {
        picker.ShownResults = new ShownPage(searched, page);
        ItemSelections.Clear(picker.Selection);
        if (page.Packages.Count > 0)
        {
            ItemSelections.Select(picker.Selection, page.Packages[0], page.Packages);
        }
    }

    private static void ReportSearchFailure(NuGetPicker picker, FeedSearch searched, Exception error)
    {
        if (!IsSamePackages(searched, FindShownSearch(picker)))
        {
            ClearShown(picker);
        }

        picker.ReportError(new AppProblem.NuGetSearchFailed(searched.Feed.Source.Name, error));
    }

    /// <summary>The icon of <paramref name="package"/>, fetched in the background the first time it is
    /// asked for; null when the package gives none.</summary>
    public static PackageIcon? EnsureIcon(NuGetPicker picker, FeedPackage package)
    {
        var url = package.IconUrl;
        if (url == null)
        {
            return null;
        }

        var setup = FindReadySetup(picker);
        if (setup == null)
        {
            return null;
        }

        if (picker.Icons.TryGetValue(url, out var icon))
        {
            return icon;
        }

        picker.IconCount++;
        icon = new PackageIcon { ImageId = IconImagePrefix + picker.IconCount };
        picker.Icons.Add(url, icon);
        FetchIcon(picker, setup, url, icon);
        return icon;
    }

    /// <summary>Gives <paramref name="icon"/> its pixels once they are in; one that cannot be fetched
    /// keeps none, so its row keeps the stand-in.</summary>
    private static void FetchIcon(NuGetPicker picker, NuGetSetup setup, WebUrl url, PackageIcon icon)
    {
        Background.Start(picker.Work, nameof(FetchIconAsync), FetchIconAsync(picker, setup, url, icon), picker.ReportError);
    }

    private static void ShowIcon(PackageIcon icon, ResourceImage? pixels)
    {
        if (pixels == null)
        {
            return;
        }

        icon.Shown = new ShownIcon(icon.ImageId, pixels, ResourceImages.IsDarkOnClear(pixels));
    }

    private static async Task FetchIconAsync(NuGetPicker picker, NuGetSetup setup, WebUrl url, PackageIcon icon)
    {
        var token = picker.Fetches.Token;
        var fetched = await Background.Wait(NuGetFeeds.FetchIconAsync(setup.Icons, url, NuGetPicker.IconPixels, token));
        if (token.IsCancellationRequested)
        {
            return;
        }

        if (fetched is Outcome<ResourceImage>.Ok ok)
        {
            ShowIcon(icon, ok.Value);
        }
    }

    /// <summary>What the feed says of <paramref name="version"/> of <paramref name="package"/>, read
    /// in the background the first time it is asked for; null while no page of packages is shown or
    /// the package lists no such version.</summary>
    public static VersionDetails? EnsureDetails(NuGetPicker picker, FeedPackage package, PackageVersion version)
    {
        if (picker.ShownResults == null)
        {
            return null;
        }

        var listed = FindVersion(package, version);
        if (listed == null)
        {
            return null;
        }

        var feed = picker.ShownResults.Search.Feed;
        var key = new DetailsKey(feed.Source.Url, package.Id, version);
        if (picker.Details.TryGetValue(key, out var details))
        {
            return details;
        }

        var loading = new VersionDetails.Loading();
        picker.Details.Add(key, loading);
        ReadDetails(picker, key, feed, listed, loading);
        return loading;
    }

    /// <summary>Reads the details of <paramref name="version"/> of <paramref name="package"/> again, after they failed.</summary>
    public static void RetryDetails(NuGetPicker picker, FeedPackage package, PackageVersion version)
    {
        if (picker.ShownResults == null)
        {
            return;
        }

        picker.Details.Remove(new DetailsKey(picker.ShownResults.Search.Feed.Source.Url, package.Id, version));
    }

    internal static FeedVersion? FindVersion(FeedPackage package, PackageVersion version)
    {
        return package.Versions.FirstOrDefault(v => v.Version == version);
    }

    /// <summary>Puts the details read in place of <paramref name="loading"/>, unless they were dropped
    /// or asked for again meanwhile.</summary>
    private static void ReadDetails(NuGetPicker picker, DetailsKey key, NuGetFeed feed, FeedVersion version, VersionDetails.Loading loading)
    {
        Background.Start(picker.Work, nameof(ReadDetailsAsync), ReadDetailsAsync(picker, key, feed, version, loading), picker.ReportError);
    }

    private static async Task ReadDetailsAsync(NuGetPicker picker, DetailsKey key, NuGetFeed feed, FeedVersion version, VersionDetails.Loading loading)
    {
        var token = picker.Fetches.Token;
        var read = await Background.Wait(NuGetFeeds.ReadDetailsAsync(feed, key.Id, version, token));
        if (token.IsCancellationRequested)
        {
            return;
        }

        if (!picker.Details.TryGetValue(key, out var current) || !ReferenceEquals(current, loading))
        {
            return;
        }

        picker.Details[key] = GetReadDetails(read);
    }

    private static VersionDetails GetReadDetails(Outcome<PackageVersionDetails> read)
    {
        return read switch
            {
                Outcome<PackageVersionDetails>.Ok ok => new VersionDetails.Read(ok.Value),
                Outcome<PackageVersionDetails>.Failed failed => new VersionDetails.Failed(failed.Error.Message),
                _ => throw new UnreachableException(),
            };
    }

    /// <summary>Offers the versions of the package selected, picking its latest, once another is selected.</summary>
    public static void FollowSelection(NuGetPicker picker)
    {
        var package = picker.Selected;
        if (package == picker.VersionsOf)
        {
            return;
        }

        picker.VersionsOf = package;
        if (package == null)
        {
            picker.Version = null;
            picker.Versions = [];
            picker.VersionTexts = [];
            return;
        }
        picker.Version = package.Version;
        picker.Versions = package.Versions
            .Select(v => v.Version)
            .ToArray();
        picker.VersionTexts = picker.Versions
            .Select(v => v.Text)
            .ToArray();
    }

    /// <summary>Fetches <see cref="Version"/> of the selected package, from the feed it was found in,
    /// into the cache, in the background, for <see cref="TakeDownloaded"/> to hand over.</summary>
    public static void DownloadPicked(NuGetPicker picker)
    {
        Background.Start(picker.Work, nameof(DownloadAsync), DownloadAsync(picker), picker.ReportError);
    }

    private static async Task DownloadAsync(NuGetPicker picker)
    {
        var version = picker.Version;
        var package = picker.Selected;
        var setup = FindReadySetup(picker);
        var shown = picker.ShownResults;
        if (!picker.CanOpen || version == null || package == null || setup == null || shown == null)
        {
            return;
        }

        var name = $"{package.Id} {version}";
        var running = new DownloadState.Running(name, new CancellationTokenSource());
        picker.DownloadRun = running;
        var fetched = await Background.Wait(NuGetFeeds.FetchPackageAsync(setup.Cache, shown.Search.Feed, package.Id, version, running.Cancel.Token));
        if (!ReferenceEquals(picker.DownloadRun, running))
        {
            return;
        }

        picker.DownloadRun = GetDownloadState(picker, name, fetched);
    }

    private static DownloadState GetDownloadState(NuGetPicker picker, string name, Outcome<string> fetched)
    {
        switch (fetched)
        {
            case Outcome<string>.Ok ok:
                return new DownloadState.Done(ok.Value);
            case Outcome<string>.Failed failed:
                picker.ReportError(new AppProblem.PackageNotDownloaded(name, failed.Error));
                return new DownloadState.None();
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>The package file the last download fetched, once; null until it is in.</summary>
    public static string? TakeDownloaded(NuGetPicker picker)
    {
        if (picker.DownloadRun is not DownloadState.Done done)
        {
            return null;
        }

        picker.DownloadRun = new DownloadState.None();
        return done.Path;
    }
}

/// <summary>A package's icon: the image the screen draws it as, and what it shows once its pixels are in.</summary>
public sealed class PackageIcon
{
    public required string ImageId { get; init; }

    /// <summary>The icon's pixels as drawn; null until they are in.</summary>
    public ShownIcon? Shown { get; set; }
}

/// <summary>An icon's pixels, drawn as the image <paramref name="ImageId"/>, with whether they are dark
/// marks on a clear background, which a dark theme would hide.</summary>
public sealed record ShownIcon(string ImageId, ResourceImage Pixels, bool IsDarkOnClear);

/// <summary>What a feed says of one version of a package: loading, read, or why it could not be.</summary>
public abstract record VersionDetails
{
    private VersionDetails()
    {
    }

    public sealed record Loading : VersionDetails;

    public sealed record Read(PackageVersionDetails Details) : VersionDetails;

    public sealed record Failed(string Error) : VersionDetails;
}

/// <summary>What the settings keep of the "open from NuGet" dialog: the feed last searched and
/// whether prereleases counted.</summary>
public sealed record SavedNuGetSearch(string FeedUrl, bool IncludePrerelease);
