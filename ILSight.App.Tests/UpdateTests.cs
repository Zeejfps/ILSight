using ZGF.Desktop;
using ZGF.Gui.Testing;
using ILSight.MainArea;
using ILSight.AppSettingArea;

namespace ILSight.App.Tests;

public sealed class UpdateTests : IDisposable
{
    private const int SettleFrames = 60;

    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    private static AppDriver Opened(FakeAppUpdatesPorts feed, bool checkAtStartup = false, ITextMeasurer? measurer = null)
    {
        var app = new AppDriver(width: 1600f, settings: new AppSettings { CheckForUpdates = checkAtStartup }, measurer: measurer, updates: feed);
        app.Driver.OpenFixtures();
        app.Run();
        return app;
    }

    private static void AwaitStatus<T>(AppDriver app) where T : UpdateStatus
    {
        app.Frames.WaitUntil(() => app.AppState.Updates.Status is T, AppStateDriver.Patience);
    }

    private static void OpenAbout(AppDriver app)
    {
        app.Ui.About.Open();
        app.Run(2);
    }

    private static void ClickCheckForUpdates(AppDriver app)
    {
        app.Frames.ClickOn(Selector.WithText(Strings.En.AboutCheckForUpdates)).Frame(2);
    }

    [Fact]
    public void A_copy_that_is_not_installed_looks_for_nothing()
    {
        var feed = new FakeAppUpdatesPorts("9.0.0") { IsInstalledCopy = false };
        var app = Opened(feed, checkAtStartup: true);

        OpenAbout(app);
        ClickCheckForUpdates(app);
        app.Run(2);

        Assert.Equal(0, feed.Finds);
        Assert.IsType<UpdateStatus.Idle>(app.AppState.Updates.Status);
    }

    [Fact]
    public void A_newer_version_found_at_startup_is_downloaded_and_offered_in_the_status_bar()
    {
        var feed = new FakeAppUpdatesPorts("9.0.0");
        var app = Opened(feed, checkAtStartup: true);

        AwaitStatus<UpdateStatus.Ready>(app);
        app.Run(2);

        Assert.NotNull(feed.Downloaded);
        Assert.True(app.Frames.Drew(Strings.En.StatusRestartToUpdate));
    }

    [Fact]
    public void Turned_off_the_startup_check_looks_for_nothing()
    {
        var feed = new FakeAppUpdatesPorts("9.0.0");
        var app = Opened(feed, checkAtStartup: false);

        app.Run(2);

        Assert.Equal(0, feed.Finds);
    }

    [Fact]
    public void A_startup_check_keeps_quiet_when_there_is_nothing_new_or_it_fails()
    {
        var nothingNew = new FakeAppUpdatesPorts(null);
        var offline = new FakeAppUpdatesPorts("9.0.0") { FailsToFind = true };

        var uneventful = Opened(nothingNew, checkAtStartup: true);
        var failing = Opened(offline, checkAtStartup: true);
        uneventful.Frames.WaitUntil(() => uneventful.AppState.Updates.Status is UpdateStatus.Idle, AppStateDriver.Patience);
        failing.Frames.WaitUntil(() => failing.AppState.Updates.Status is UpdateStatus.Idle, AppStateDriver.Patience);

        Assert.Equal((1, 1), (nothingNew.Finds, offline.Finds));
        Assert.Empty(failing.AppState.Errors.Entries);
    }

    [Fact]
    public void Asking_says_when_this_is_the_newest_version()
    {
        var app = Opened(new FakeAppUpdatesPorts(null));
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
        var app = Opened(new FakeAppUpdatesPorts("9.0.0") { FailsToFind = failsToFind, FailsToDownload = failsToDownload });
        OpenAbout(app);

        ClickCheckForUpdates(app);
        AwaitStatus<UpdateStatus.Failed>(app);
        app.Run(2);

        Assert.True(app.Frames.Drew(Strings.En.AboutUpdateFailed));
        Assert.True(app.AppState.Updates.CanCheck());
    }

    [Theory]
    [MemberData(nameof(AllLocales))]
    public void What_came_of_asking_leaves_the_about_dialog_as_tall_as_it_was(Locale locale)
    {
        var uneventful = Opened(new FakeAppUpdatesPorts(null), measurer: new AppFontWidths());
        var failing = Opened(new FakeAppUpdatesPorts("9.0.0") { FailsToFind = true }, measurer: new AppFontWidths());

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
        {
            locales.Add(locale);
        }

        return locales;
    }

    private static float OpenAboutIn(AppDriver app, Locale locale)
    {
        app.Frames.WindowIcon = new WindowIcon([new WindowIconImage(16, 16, new byte[16 * 16 * 4])]);
        app.AppState.Locale = locale;
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
        var feed = new FakeAppUpdatesPorts("9.0.0");
        var app = Opened(feed);
        OpenAbout(app);
        ClickCheckForUpdates(app);
        AwaitStatus<UpdateStatus.Ready>(app);
        app.Run(2);

        app.Frames.ClickOn(Selector.WithText(Strings.En.AboutRestartToUpdate("9.0.0"))).Frame(2);
        app.Frames.WaitUntil(() => app.Frames.QuitRequested, AppStateDriver.Patience);

        Assert.Same(feed.Downloaded, feed.Applied);
    }

    [Fact]
    public void A_restart_the_updater_could_not_start_leaves_the_app_open_and_says_so()
    {
        var app = Opened(new FakeAppUpdatesPorts("9.0.0") { FailsToApply = true });
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
        Assert.True(app.AppState.IsUpdateCheckAutomatic);

        OpenAbout(app);
        app.Frames.ClickOn(Selector.WithText(Strings.En.AboutCheckAutomatically)).Frame(2);
        app.Run(2);

        Assert.False(_temp.Settings().CheckForUpdates);
        Assert.False(new AppDriver(settings: _temp.Settings()).AppState.IsUpdateCheckAutomatic);
    }
}
