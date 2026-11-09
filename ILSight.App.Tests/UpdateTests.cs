using Velopack;
using ZGF.Desktop;
using ZGF.Gui;
using ZGF.Gui.Testing;
using ILSight.Localization;
using static ILSight.Preferences;
using static ILSight.Updates;

namespace ILSight.App.Tests;

public sealed class UpdateTests : IDisposable
{
    private const int SettleFrames = 60;

    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    /// <summary>A feed that answers from memory, and can be made to fail at any step.</summary>
    private sealed class FakeFeed(string? newest)
    {
        public bool IsInstalled { get; init; } = true;
        public bool FailsToFind { get; init; }
        public bool FailsToDownload { get; init; }
        public bool FailsToApply { get; init; }
        public int Finds { get; private set; }
        public UpdateInfo? Downloaded { get; private set; }
        public UpdateInfo? Applied { get; private set; }

        public UpdateInfo? FindUpdate()
        {
            Finds++;
            if (FailsToFind)
                throw new HttpRequestException("Offline");
            if (newest == null)
                return null;
            return new UpdateInfo(new VelopackAsset { Version = SemanticVersion.Parse(newest) }, false);
        }

        public void Download(UpdateInfo update)
        {
            if (FailsToDownload)
                throw new IOException("Disk full");
            Downloaded = update;
        }

        public void ApplyAfterExit(UpdateInfo update)
        {
            if (FailsToApply)
                throw new IOException("Updater missing");
            Applied = update;
        }
    }

    /// <summary>As wide as the app's font draws Latin text, which the default 8px a character overstates:
    /// the test of what fits has to go by what the window would show.</summary>
    private sealed class AppFontWidths : ITextMeasurer
    {
        private const float CharacterWidth = 6.5f;
        private const float LineHeight = 16f;

        public float MeasureTextWidth(ReadOnlySpan<char> text, TextStyle style) => text.Length * CharacterWidth;

        public float MeasureTextPrefix(ReadOnlySpan<char> text, int prefixLength, TextStyle style) =>
            Math.Clamp(prefixLength, 0, text.Length) * CharacterWidth;

        public float MeasureTextLineHeight(TextStyle style) => LineHeight;
    }

    private static AppDriver Opened(FakeFeed feed, bool checkAutomatically = true, ITextMeasurer? measurer = null)
    {
        var app = AppDriver.Opened(width: 1600f, measurer: measurer);
        app.AppState.Updates = new AppUpdates
        {
            Feed = new UpdateFeed(() => feed.IsInstalled, feed.FindUpdate, feed.Download, feed.ApplyAfterExit),
            CheckAutomatically = checkAutomatically,
        };
        app.Run();
        return app;
    }

    private static void AwaitStatus<T>(AppDriver app) where T : UpdateStatus =>
        app.Frames.WaitUntil(() => app.AppState.Updates.Status is T, Patience);

    private static void OpenAbout(AppDriver app)
    {
        Dialogs.OpenAbout(app.Ui);
        app.Run(2);
    }

    private static void ClickCheckForUpdates(AppDriver app)
    {
        app.Frames.ClickOn(Selector.WithText(Strings.En.AboutCheckForUpdates)).Frame(2);
    }

    [Fact]
    public void A_copy_that_is_not_installed_looks_for_nothing()
    {
        var feed = new FakeFeed("9.0.0") { IsInstalled = false };
        var app = Opened(feed);

        CheckOnStartup(app.AppState);
        OpenAbout(app);
        ClickCheckForUpdates(app);
        app.Run(2);

        Assert.Equal(0, feed.Finds);
        Assert.IsType<UpdateStatus.Idle>(app.AppState.Updates.Status);
    }

    [Fact]
    public void A_newer_version_found_at_startup_is_downloaded_and_offered_in_the_status_bar()
    {
        var feed = new FakeFeed("9.0.0");
        var app = Opened(feed);

        CheckOnStartup(app.AppState);
        AwaitStatus<UpdateStatus.Ready>(app);
        app.Run(2);

        Assert.NotNull(feed.Downloaded);
        Assert.True(app.Frames.Drew(Strings.En.StatusRestartToUpdate));
    }

    [Fact]
    public void Turned_off_the_startup_check_looks_for_nothing()
    {
        var feed = new FakeFeed("9.0.0");
        var app = Opened(feed, checkAutomatically: false);

        CheckOnStartup(app.AppState);
        app.Run(2);

        Assert.Equal(0, feed.Finds);
    }

    [Fact]
    public void A_startup_check_keeps_quiet_when_there_is_nothing_new_or_it_fails()
    {
        var uneventful = Opened(new FakeFeed(null));
        var failing = Opened(new FakeFeed("9.0.0") { FailsToFind = true });

        CheckOnStartup(uneventful.AppState);
        CheckOnStartup(failing.AppState);
        uneventful.Frames.WaitUntil(() => uneventful.AppState.Updates.Status is UpdateStatus.Idle, Patience);
        failing.Frames.WaitUntil(() => failing.AppState.Updates.Status is UpdateStatus.Idle, Patience);

        Assert.Empty(failing.AppState.Errors.Entries);
    }

    [Fact]
    public void Asking_says_when_this_is_the_newest_version()
    {
        var app = Opened(new FakeFeed(null));
        OpenAbout(app);

        ClickCheckForUpdates(app);
        AwaitStatus<UpdateStatus.UpToDate>(app);
        app.Run(2);

        Assert.True(app.Frames.Drew(Strings.En.AboutUpToDate));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Asking_says_when_the_check_or_the_download_failed_and_can_be_tried_again(bool failsToFind, bool failsToDownload)
    {
        var app = Opened(new FakeFeed("9.0.0") { FailsToFind = failsToFind, FailsToDownload = failsToDownload });
        OpenAbout(app);

        ClickCheckForUpdates(app);
        AwaitStatus<UpdateStatus.Failed>(app);
        app.Run(2);

        Assert.True(app.Frames.Drew(Strings.En.AboutUpdateFailed));
        Assert.True(CanCheck(app.AppState.Updates));
    }

    [Theory]
    [MemberData(nameof(AllLocales))]
    public void What_came_of_asking_leaves_the_about_dialog_as_tall_as_it_was(Locale locale)
    {
        var uneventful = Opened(new FakeFeed(null), measurer: new AppFontWidths());
        var failing = Opened(new FakeFeed("9.0.0") { FailsToFind = true }, measurer: new AppFontWidths());

        var closeBefore = OpenAboutIn(uneventful, locale);
        OpenAboutIn(failing, locale);
        Ask<UpdateStatus.UpToDate>(uneventful);
        Ask<UpdateStatus.Failed>(failing);

        var t = Strings.For(locale);
        Assert.True(uneventful.Frames.Drew(t.AboutUpToDate));
        Assert.True(failing.Frames.Drew(t.AboutUpdateFailed));
        Assert.Equal(closeBefore, uneventful.TextRect(t.AboutClose).Y);
        Assert.Equal(closeBefore, failing.TextRect(t.AboutClose).Y);
    }

    public static TheoryData<Locale> AllLocales()
    {
        var locales = new TheoryData<Locale>();
        foreach (var (locale, _) in Locales.All)
            locales.Add(locale);
        return locales;
    }

    /// <summary>Opens the dialog in <paramref name="locale"/> and lets it settle; where Close is then.</summary>
    private static float OpenAboutIn(AppDriver app, Locale locale)
    {
        app.Frames.WindowIcon = new WindowIcon([new WindowIconImage(16, 16, new byte[16 * 16 * 4])]);
        SetLocale(app.AppState, locale);
        app.Run(2);
        OpenAbout(app);
        app.Run(SettleFrames);
        return app.TextRect(app.Ui.Strings.AboutClose).Y;
    }

    private static void Ask<T>(AppDriver app) where T : UpdateStatus
    {
        app.Frames.ClickOn(Selector.WithText(app.Ui.Strings.AboutCheckForUpdates)).Frame(2);
        AwaitStatus<T>(app);
        app.Run(2);
    }

    [Fact]
    public void Restarting_starts_the_updater_and_then_closes_the_app()
    {
        var feed = new FakeFeed("9.0.0");
        var app = Opened(feed);
        OpenAbout(app);
        ClickCheckForUpdates(app);
        AwaitStatus<UpdateStatus.Ready>(app);
        app.Run(2);

        app.Frames.ClickOn(Selector.WithText(Strings.En.AboutRestartToUpdate("9.0.0"))).Frame(2);
        app.Frames.WaitUntil(() => app.Frames.QuitRequested, Patience);

        Assert.Same(feed.Downloaded, feed.Applied);
    }

    [Fact]
    public void A_restart_the_updater_could_not_start_leaves_the_app_open_and_says_so()
    {
        var app = Opened(new FakeFeed("9.0.0") { FailsToApply = true });
        OpenAbout(app);
        ClickCheckForUpdates(app);
        AwaitStatus<UpdateStatus.Ready>(app);
        app.Run(2);

        app.Frames.ClickOn(Selector.WithText(Strings.En.AboutRestartToUpdate("9.0.0"))).Frame(2);
        AwaitStatus<UpdateStatus.Failed>(app);
        app.Run(2);

        Assert.False(app.Frames.QuitRequested);
        Assert.True(app.Frames.Drew(Strings.En.AboutUpdateFailed));
    }

    [Fact]
    public void The_startup_check_is_on_unless_turned_off_and_stays_as_it_was_set()
    {
        var app = new AppDriver(width: 1600f, settings: _temp.Settings());
        app.Run(2);
        Assert.True(app.AppState.Updates.CheckAutomatically);

        OpenAbout(app);
        app.Frames.ClickOn(Selector.WithText(Strings.En.AboutCheckAutomatically)).Frame(2);
        app.Run(2);

        Assert.False(_temp.Settings().CheckForUpdates);
        Assert.False(new AppDriver(settings: _temp.Settings()).AppState.Updates.CheckAutomatically);
    }
}
