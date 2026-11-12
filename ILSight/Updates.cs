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

/// <summary>What the app knows of its updates, and looking for a newer version, downloading it and handing it
/// to the updater. Lives on the UI thread, as the app state does. An update nobody restarts for is applied by the
/// next launch. Its workflows return once their work is done, and reach the releases through
/// <see cref="IPorts"/>.</summary>
public sealed class AppUpdates(AppUpdates.IPorts ports)
{
    /// <summary>Where newer versions come from.</summary>
    public interface IPorts
    {
        /// <summary>Whether this copy can update itself: not one run from the IDE or a build folder.</summary>
        bool IsInstalled();

        /// <summary>The newest version if it is newer than this one, else null, looked for without holding up the
        /// screen; looking that failed comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<UpdateInfo?>> LookUpUpdateAsync(CancellationToken ct);

        /// <summary>Downloads <paramref name="update"/> without holding up the screen; false when that failed.</summary>
        Task<bool> TryDownloadAsync(UpdateInfo update, CancellationToken ct);

        /// <summary>Starts the updater, which replaces this version with <paramref name="update"/> and opens it
        /// once this process has ended; false when it could not be started.</summary>
        Task<bool> TryApplyAfterExitAsync(UpdateInfo update, CancellationToken ct);
    }

    private UpdateCheck? _check;
    private UpdateRestart? _restart;

    public UpdateStatus Status { get; private set; } = new UpdateStatus.Idle();

    public static string GetVersion(UpdateInfo update)
    {
        return update.TargetFullRelease.Version.ToString();
    }

    /// <summary>Whether a check can start: this copy can update itself, and no check, download or restart is
    /// under way or ready.</summary>
    public bool CanCheck()
    {
        return ports.IsInstalled() && Status is UpdateStatus.Idle or UpdateStatus.UpToDate or UpdateStatus.Failed;
    }

    /// <summary>Starts looking for a newer version, unless one can't be looked for now (see
    /// <see cref="CanCheck"/>): the status says it is checking from now on. Only a check the user
    /// <paramref name="asked"/> for says when there is none or it failed: nobody is waiting on the one at startup.
    /// Returns what <see cref="CheckAsync"/> needs to look; null when no check started.</summary>
    public UpdateCheck? StartCheck(bool asked)
    {
        if (!CanCheck())
        {
            return null;
        }

        var check = new UpdateCheck { Asked = asked };
        _check = check;
        Status = new UpdateStatus.Checking();
        return check;
    }

    /// <summary>Looks for a newer version and downloads it, for <paramref name="check"/>, which
    /// <see cref="StartCheck"/> started; a check it did not start does nothing.</summary>
    public async Task CheckAsync(UpdateCheck check, CancellationToken ct)
    {
        if (check != _check)
        {
            return;
        }

        var asked = check.Asked;
        var found = await ports.LookUpUpdateAsync(ct);
        if (found is not Outcome<UpdateInfo?>.Ok ok)
        {
            Status = FailedTo(asked);
            return;
        }
        if (ok.Value is null)
        {
            Status = asked ? new UpdateStatus.UpToDate() : new UpdateStatus.Idle();
            return;
        }
        await DownloadAsync(ok.Value, asked, ct);
    }

    private async Task DownloadAsync(UpdateInfo update, bool asked, CancellationToken ct)
    {
        Status = new UpdateStatus.Downloading(GetVersion(update));
        var downloaded = await ports.TryDownloadAsync(update, ct);
        Status = downloaded ? new UpdateStatus.Ready(update) : FailedTo(asked);
    }

    private static UpdateStatus FailedTo(bool asked)
    {
        return asked ? new UpdateStatus.Failed() : new UpdateStatus.Idle();
    }

    /// <summary>Starts restarting into the update that is ready, if one is: the status says it is restarting from
    /// now on. Returns what <see cref="RestartToUpdateAsync"/> needs to hand it over; null when none is
    /// ready.</summary>
    public UpdateRestart? StartRestart()
    {
        if (Status is not UpdateStatus.Ready ready)
        {
            return null;
        }

        var restart = new UpdateRestart { Update = ready.Update };
        _restart = restart;
        Status = new UpdateStatus.Restarting();
        return restart;
    }

    /// <summary>Hands the update <paramref name="restart"/> is of to the updater, then has the app close the
    /// normal way, through <paramref name="viewCommands"/>, so that it saves what it has, into the update. An
    /// updater that could not be started leaves the app open, and says so. A restart <see cref="StartRestart"/>
    /// did not start does nothing.</summary>
    public async Task RestartToUpdateAsync(UpdateRestart restart, Queue<ViewCommand> viewCommands, CancellationToken ct)
    {
        if (restart != _restart)
        {
            return;
        }

        var started = await ports.TryApplyAfterExitAsync(restart.Update, ct);
        if (!started)
        {
            Status = new UpdateStatus.Failed();
            return;
        }

        viewCommands.Enqueue(new ViewCommand.Quit());
    }
}

/// <summary>Given for one check by <see cref="AppUpdates.StartCheck"/>: whether the user asked for it.</summary>
public sealed class UpdateCheck
{
    public required bool Asked { get; init; }
}

/// <summary>Given for one restart by <see cref="AppUpdates.StartRestart"/>: the update it hands over.</summary>
public sealed class UpdateRestart
{
    public required UpdateInfo Update { get; init; }
}

/// <summary>The seam around Velopack: the releases of the app's GitHub repository, on the channel it was
/// packed for, for <see cref="AppUpdates"/>. The channel each RID is packed with in release.yml comes with the
/// install, so it is not named here. Velopack's calls block, so they run on the thread pool and come back on the
/// UI thread; what they throw is a failed result, except the cancellation of the token the call was given.</summary>
public sealed class AppUpdatesAdapter : AppUpdates.IPorts
{
    // Every installed copy asks here for the rest of its life, so the repository can't move without
    // leaving a redirect behind.
    private const string Repository = "https://github.com/Zeejfps/ILSight";

    // The manager can only be made once VelopackApp has run, which Program.cs does first; a process
    // that started some other way has nothing to update.
    private readonly Lazy<UpdateManager> _manager = new(static () => new UpdateManager(new GithubSource(Repository, null, prerelease: false)));

    public bool IsInstalled()
    {
        return VelopackLocator.IsCurrentSet && _manager.Value.IsInstalled;
    }

    public async Task<Outcome<UpdateInfo?>> LookUpUpdateAsync(CancellationToken ct)
    {
        var found = await Background.Wait(Task.Run(() => _manager.Value.CheckForUpdates(), ct));
        ct.ThrowIfCancellationRequested();
        return found;
    }

    public async Task<bool> TryDownloadAsync(UpdateInfo update, CancellationToken ct)
    {
        var downloaded = await Background.Wait(Task.Run(() => _manager.Value.DownloadUpdates(update), ct));
        ct.ThrowIfCancellationRequested();
        return downloaded is Outcome.Done;
    }

    public async Task<bool> TryApplyAfterExitAsync(UpdateInfo update, CancellationToken ct)
    {
        var started = await Background.Wait(Task.Run(() => _manager.Value.WaitExitThenApplyUpdates(update, silent: false, restart: true), ct));
        ct.ThrowIfCancellationRequested();
        return started is Outcome.Done;
    }
}
