using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace ILSight;

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

    /// <summary>Starting the updater can't be undone, and the app must quit after it (R009): <paramref name="ct"/> stops
    /// the call only before the updater starts. Once it has started, the answer is that it did, whatever the token,
    /// so that the app quits into the update rather than leave an updater waiting for an exit.</summary>
    public async Task<bool> TryApplyAfterExitAsync(UpdateInfo update, CancellationToken ct)
    {
        var started = await Background.Wait(Task.Run(() => _manager.Value.WaitExitThenApplyUpdates(update, silent: false, restart: true), ct));
        if (started is Outcome.Done)
        {
            return true;
        }

        ct.ThrowIfCancellationRequested();
        return false;
    }
}
