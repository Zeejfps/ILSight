using Velopack;
using ILSight.Localization;
using static ILSight.Updates;

namespace ILSight.App.Tests;

public sealed class UpdateTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    /// <summary>A feed that answers from memory, and can be made to fail at any step.</summary>
    private sealed class FakeFeed(string? newest) : IUpdateFeed
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

    private static AppDriver Opened(FakeFeed feed, bool checkAutomatically = true)
    {
        var app = AppDriver.Opened(width: 1600f);
        app.AppState.Updates = new AppUpdates(feed, checkAutomatically);
        app.Run();
        return app;
    }

    private static void AwaitStatus<T>(AppDriver app) where T : UpdateStatus =>
        app.RunUntil(() => app.AppState.Updates.Status is T);

    private static void OpenAbout(AppDriver app)
    {
        Dialogs.OpenAbout(app.Ui);
        app.Run(2);
    }

    private static void ClickCheckForUpdates(AppDriver app)
    {
        app.Click(Strings.En.AboutCheckForUpdates).Frame(2);
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
        uneventful.RunUntil(() => uneventful.AppState.Updates.Status is UpdateStatus.Idle);
        failing.RunUntil(() => failing.AppState.Updates.Status is UpdateStatus.Idle);

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
        Assert.True(app.AppState.Updates.CanCheck);
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

        app.Click(Strings.En.AboutRestartToUpdate("9.0.0")).Frame(2);
        app.RunUntil(() => app.Frames.QuitRequested);

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

        app.Click(Strings.En.AboutRestartToUpdate("9.0.0")).Frame(2);
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
        app.Click(Strings.En.AboutCheckAutomatically).Frame(2);
        app.Run(2);

        Assert.False(_temp.Settings().CheckForUpdates);
        Assert.False(new AppDriver(settings: _temp.Settings()).AppState.Updates.CheckAutomatically);
    }
}
