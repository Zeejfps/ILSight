using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace ILSight;

/// <summary>Where the app stands with its newer versions.</summary>
public abstract record UpdateStatus
{
    private UpdateStatus()
    {
    }

    /// <summary>Nothing has been looked for yet, or an automatic check found nothing to say.</summary>
    public sealed record Idle : UpdateStatus;

    public sealed record Checking : UpdateStatus;

    public sealed record Downloading(string Version) : UpdateStatus;

    /// <summary>A newer version is downloaded and takes over when the app restarts.</summary>
    public sealed record Ready(UpdateInfo Update) : UpdateStatus
    {
        public string Version => VersionOf(Update);
    }

    public sealed record Restarting : UpdateStatus;

    /// <summary>The user asked, and this is the newest version.</summary>
    public sealed record UpToDate : UpdateStatus;

    /// <summary>What the user asked for could not be done; Velopack's log says why.</summary>
    public sealed record Failed : UpdateStatus;

    internal static string VersionOf(UpdateInfo update) => update.TargetFullRelease.Version.ToString();
}

/// <summary>Where newer versions come from. The calls block, so they run in the background.</summary>
public interface IUpdateFeed
{
    /// <summary>Whether this copy can update itself: not one run from the IDE or a build folder.</summary>
    bool IsInstalled { get; }

    /// <summary>The newest version if it is newer than this one, else null.</summary>
    UpdateInfo? FindUpdate();

    void Download(UpdateInfo update);

    /// <summary>Starts the updater, which replaces this version with <paramref name="update"/> and opens
    /// it once this process has ended; the app is then to close as it normally does.</summary>
    void ApplyAfterExit(UpdateInfo update);
}

/// <summary>The releases of the app's GitHub repository, on the channel it was packed for: the channel
/// each RID is packed with in release.yml comes with the install, so it is not named here.</summary>
public sealed class GitHubUpdateFeed : IUpdateFeed
{
    // Every installed copy asks here for the rest of its life, so the repository can't move without
    // leaving a redirect behind.
    private const string Repository = "https://github.com/Zeejfps/ILSight";

    private readonly Lazy<UpdateManager> _manager = new(static () => new UpdateManager(new GithubSource(Repository, null, prerelease: false)));

    /// <summary>The manager can only be made once <c>VelopackApp</c> has run, which Program.cs does
    /// first; a process that started some other way has nothing to update.</summary>
    public bool IsInstalled => VelopackLocator.IsCurrentSet && _manager.Value.IsInstalled;

    public UpdateInfo? FindUpdate() => _manager.Value.CheckForUpdates();

    public void Download(UpdateInfo update) => _manager.Value.DownloadUpdates(update);

    public void ApplyAfterExit(UpdateInfo update) =>
        _manager.Value.WaitExitThenApplyUpdates(update, silent: false, restart: true);
}

/// <summary>Looks for a newer version, downloads it and offers it. Lives on the UI thread, as the app
/// state does. An update nobody restarts for is applied by the next launch.</summary>
public sealed class AppUpdates(IUpdateFeed feed, bool checkAutomatically)
{
    public UpdateStatus Status { get; private set; } = new UpdateStatus.Idle();

    /// <summary>Whether the app looks for a newer version when it starts.</summary>
    public bool CheckAutomatically { get; set; } = checkAutomatically;

    public bool CanCheck => feed.IsInstalled && Status is UpdateStatus.Idle or UpdateStatus.UpToDate or UpdateStatus.Failed;

    /// <summary>Looks for a newer version and downloads it. Only one the user <paramref name="asked"/>
    /// for says when there is none or it failed: nobody is waiting on the one at startup.</summary>
    public async Task Check(bool asked)
    {
        if (!CanCheck)
            return;
        Status = new UpdateStatus.Checking();
        var found = await Background.Run(feed.FindUpdate);
        if (found is not Outcome<UpdateInfo?>.Ok ok)
        {
            Status = FailedTo(asked);
            return;
        }
        if (ok.Value == null)
        {
            Status = asked ? new UpdateStatus.UpToDate() : new UpdateStatus.Idle();
            return;
        }
        await Download(ok.Value, asked);
    }

    private async Task Download(UpdateInfo update, bool asked)
    {
        Status = new UpdateStatus.Downloading(UpdateStatus.VersionOf(update));
        var downloaded = await Background.Run(() =>
        {
            feed.Download(update);
            return update;
        });
        Status = downloaded is Outcome<UpdateInfo>.Ok ? new UpdateStatus.Ready(update) : FailedTo(asked);
    }

    private static UpdateStatus FailedTo(bool asked) => asked ? new UpdateStatus.Failed() : new UpdateStatus.Idle();

    /// <summary>Hands the update that is ready to the updater; true when the app is then to close.</summary>
    public async Task<bool> ApplyAfterExit()
    {
        if (Status is not UpdateStatus.Ready ready)
            return false;
        Status = new UpdateStatus.Restarting();
        var update = ready.Update;
        var started = await Background.Run(() =>
        {
            feed.ApplyAfterExit(update);
            return update;
        });
        if (started is Outcome<UpdateInfo>.Ok)
            return true;
        Status = new UpdateStatus.Failed();
        return false;
    }
}

/// <summary>What the user can do about updates.</summary>
public static class Updates
{
    /// <summary>Looks for a newer version unless the user turned that off.</summary>
    public static void CheckOnStartup(AppState appState)
    {
        if (appState.Updates.CheckAutomatically)
            Background.Start(appState.Updates.Check(asked: false), appState.ReportError);
    }

    public static void CheckForUpdates(AppState appState) =>
        Background.Start(appState.Updates.Check(asked: true), appState.ReportError);

    public static void SetCheckAutomatically(AppState appState, bool check) =>
        appState.Updates.CheckAutomatically = check;

    /// <summary>Closes the app the normal way, so that it saves what it has, into the update.</summary>
    public static void RestartToUpdate(AppState appState) =>
        Background.Start(RestartToUpdateAsync(appState), appState.ReportError);

    private static async Task RestartToUpdateAsync(AppState appState)
    {
        if (await appState.Updates.ApplyAfterExit())
            appState.EnqueueCommand(new ViewCommand.Quit());
    }
}
