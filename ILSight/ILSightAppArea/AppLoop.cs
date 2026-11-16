using ILSight.AssemblyArea;
using ILSight.UserArea;

namespace ILSight.ILSightAppArea;

/// <summary>The app's loop, the one sequence every host that runs the whole app runs: its start, and the calls before
/// and after each frame draws. The composition root builds it (<see cref="AppRoot.BuildLoop"/>); the window
/// (<see cref="ILSightApp"/>) owns one, its startup hook calling <see cref="Start"/> after its own setup and each
/// frame calling <see cref="BeforeDraw"/>, the draw, <see cref="AfterDraw"/>, then its own chores; the test hosts build
/// one and run the same. Building starts nothing.</summary>
[Edge]
public sealed class AppLoop
{
    private readonly AppState _appState;
    private readonly ListStart _start;
    private readonly RunArgumentsTaking _runArguments;
    private readonly AssemblyListLoading _listLoading;
    private readonly UserThemeWatching _themeWatching;
    private readonly UpdateChecking _updateChecking;

    /// <param name="appState">The app state the root built, its start recorded on it.</param>
    /// <param name="start">The start the root recorded on <paramref name="appState"/>: the active list's reopening
    /// begun, nothing read.</param>
    /// <param name="runArguments">Takes the arguments of this run and of later ones.</param>
    /// <param name="listLoading">Loads the open list's assemblies.</param>
    /// <param name="themeWatching">Watches the user's theme folders.</param>
    /// <param name="updateChecking">Looks for a newer version as the app starts.</param>
    internal AppLoop(AppState appState, ListStart start, RunArgumentsTaking runArguments, AssemblyListLoading listLoading,
        UserThemeWatching themeWatching, UpdateChecking updateChecking)
    {
        _appState = appState;
        _start = start;
        _runArguments = runArguments;
        _listLoading = listLoading;
        _themeWatching = themeWatching;
        _updateChecking = updateChecking;
    }

    /// <summary>Starts what the app runs from its start until it shuts down: takes the run arguments already waiting,
    /// this run's first, then starts the reading of the list it starts with, the taking of later runs' arguments, the
    /// watch of the user's theme folders, and the look for a newer version, unless the user turned it off.</summary>
    public void Start()
    {
        // The arguments before the list's reading: they decide from the list as on its way (a switch remembers it, a
        // clear drops its files on their way), so their prerequisite is the start recorded, the reopening begun and
        // nothing read; taken first, a switch or a clear stops that reading before it reads a file of the list it leaves.
        _runArguments.TakeWaiting();
        _listLoading.StartOpeningStartupList(_start);
        _runArguments.StartTakingLaterRuns();
        _themeWatching.StartWatching();
        _updateChecking.CheckIfAutomatic(_appState.IsUpdateCheckAutomatic);
    }

    /// <summary>What comes before <paramref name="screen"/> draws a frame in <paramref name="frame"/>.</summary>
    public void BeforeDraw(UiState screen, IFrameHost frame)
    {
        AppFrames.StartFrame(screen, _appState);
        AppFrames.FollowNuGetPicker(frame, screen.AppCommands, screen.NuGetPicker, screen.SearchNuGetFeedHandler, screen.FetchNuGetIconHandler,
            screen.ReadNuGetDetailsHandler, _appState);
    }

    /// <summary>What comes after <paramref name="screen"/> drew a frame in <paramref name="frame"/>, the draw failing
    /// or not; returns what the autosave came to, for a host with frames to come back when a write waits or
    /// failed.</summary>
    public Saving AfterDraw(UiState screen, IFrameHost frame)
    {
        AppFrames.KeepScreenChanges(screen, _appState);
        AppFrames.StartAskedWork(screen, _appState);
        AppFrames.RunAppCommandsAndRedraw(frame, screen.AppCommands, _appState);
        AppFrames.RequestUpdateIfViewCommandsPending(frame, _appState);
        return AppFrames.SaveChangedSettings(frame, _appState);
    }
}
