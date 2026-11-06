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
    public sealed record Ready(UpdateInfo Update) : UpdateStatus;

    public sealed record Restarting : UpdateStatus;

    /// <summary>The user asked, and this is the newest version.</summary>
    public sealed record UpToDate : UpdateStatus;

    /// <summary>What the user asked for could not be done; Velopack's log says why.</summary>
    public sealed record Failed : UpdateStatus;
}

/// <summary>Where newer versions come from. The calls block, so they run in the background.</summary>
/// <param name="IsInstalled">Whether this copy can update itself: not one run from the IDE or a build folder.</param>
/// <param name="FindUpdate">The newest version if it is newer than this one, else null.</param>
/// <param name="Download">Downloads an update found.</param>
/// <param name="ApplyAfterExit">Starts the updater, which replaces this version with the update and opens
/// it once this process has ended; the app is then to close as it normally does.</param>
public sealed record UpdateFeed(
    Func<bool> IsInstalled,
    Func<UpdateInfo?> FindUpdate,
    Action<UpdateInfo> Download,
    Action<UpdateInfo> ApplyAfterExit);

/// <summary>The seam around Velopack: the releases of the app's GitHub repository, on the channel it was
/// packed for. The channel each RID is packed with in release.yml comes with the install, so it is not
/// named here.</summary>
public static class GitHubUpdates
{
    // Every installed copy asks here for the rest of its life, so the repository can't move without
    // leaving a redirect behind.
    private const string Repository = "https://github.com/Zeejfps/ILSight";

    public static UpdateFeed CreateFeed()
    {
        // The manager can only be made once VelopackApp has run, which Program.cs does first; a process
        // that started some other way has nothing to update.
        var manager = new Lazy<UpdateManager>(static () => new UpdateManager(new GithubSource(Repository, null, prerelease: false)));
        return new UpdateFeed(
            IsInstalled: () =>
            {
                return VelopackLocator.IsCurrentSet && manager.Value.IsInstalled;
            },
            FindUpdate: () =>
            {
                return manager.Value.CheckForUpdates();
            },
            Download: update =>
            {
                manager.Value.DownloadUpdates(update);
            },
            ApplyAfterExit: update =>
            {
                manager.Value.WaitExitThenApplyUpdates(update, silent: false, restart: true);
            });
    }
}

/// <summary>What the app knows of its updates. Lives on the UI thread, as the app state does. An update
/// nobody restarts for is applied by the next launch.</summary>
/// <param name="feed">Where newer versions come from.</param>
/// <param name="checkAutomatically">Whether the app looks for a newer version when it starts.</param>
public sealed class AppUpdates(UpdateFeed feed, bool checkAutomatically)
{
    public UpdateFeed Feed { get; } = feed;

    public UpdateStatus Status { get; set; } = new UpdateStatus.Idle();

    public bool CheckAutomatically { get; set; } = checkAutomatically;
}

/// <summary>Looking for a newer version, downloading it and offering it, and what the user can do about
/// that.</summary>
public static class Updates
{
    public static string GetVersion(UpdateInfo update)
    {
        return update.TargetFullRelease.Version.ToString();
    }

    public static bool CanCheck(AppUpdates updates)
    {
        return updates.Feed.IsInstalled() && updates.Status is UpdateStatus.Idle or UpdateStatus.UpToDate or UpdateStatus.Failed;
    }

    /// <summary>Looks for a newer version and downloads it. Only one the user <paramref name="asked"/>
    /// for says when there is none or it failed: nobody is waiting on the one at startup.</summary>
    public static async Task Check(AppUpdates updates, bool asked)
    {
        if (!CanCheck(updates))
            return;
        updates.Status = new UpdateStatus.Checking();
        var found = await Background.Run(updates.Feed.FindUpdate);
        if (found is not Outcome<UpdateInfo?>.Ok ok)
        {
            updates.Status = FailedTo(asked);
            return;
        }
        if (ok.Value == null)
        {
            updates.Status = asked ? new UpdateStatus.UpToDate() : new UpdateStatus.Idle();
            return;
        }
        await Download(updates, ok.Value, asked);
    }

    private static async Task Download(AppUpdates updates, UpdateInfo update, bool asked)
    {
        updates.Status = new UpdateStatus.Downloading(GetVersion(update));
        var downloaded = await Background.Run(() =>
        {
            updates.Feed.Download(update);
            return update;
        });
        updates.Status = downloaded is Outcome<UpdateInfo>.Ok ? new UpdateStatus.Ready(update) : FailedTo(asked);
    }

    private static UpdateStatus FailedTo(bool asked)
    {
        return asked ? new UpdateStatus.Failed() : new UpdateStatus.Idle();
    }

    /// <summary>Hands the update that is ready to the updater; true when the app is then to close.</summary>
    public static async Task<bool> ApplyAfterExit(AppUpdates updates)
    {
        if (updates.Status is not UpdateStatus.Ready ready)
            return false;
        updates.Status = new UpdateStatus.Restarting();
        var update = ready.Update;
        var started = await Background.Run(() =>
        {
            updates.Feed.ApplyAfterExit(update);
            return update;
        });
        if (started is Outcome<UpdateInfo>.Ok)
            return true;
        updates.Status = new UpdateStatus.Failed();
        return false;
    }

    /// <summary>Looks for a newer version unless the user turned that off.</summary>
    public static void CheckOnStartup(AppState appState)
    {
        if (appState.Updates.CheckAutomatically)
            Background.Start(Check(appState.Updates, asked: false), appState.ReportError);
    }

    public static void CheckForUpdates(AppState appState)
    {
        Background.Start(Check(appState.Updates, asked: true), appState.ReportError);
    }

    public static void SetCheckAutomatically(AppState appState, bool check)
    {
        appState.Updates.CheckAutomatically = check;
    }

    /// <summary>Closes the app the normal way, so that it saves what it has, into the update.</summary>
    public static void RestartToUpdate(AppState appState)
    {
        Background.Start(RestartToUpdateAsync(appState), appState.ReportError);
    }

    private static async Task RestartToUpdateAsync(AppState appState)
    {
        if (await ApplyAfterExit(appState.Updates))
            appState.EnqueueCommand(new ViewCommand.Quit());
    }
}
