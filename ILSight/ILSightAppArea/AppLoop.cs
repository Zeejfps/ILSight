using System.Threading.Channels;

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
    private readonly Channel<RunArguments> _inbox;

    /// <param name="appState">The app state the root built, its start recorded on it.</param>
    /// <param name="start">The start the root recorded on <paramref name="appState"/>.</param>
    /// <param name="inbox">Where the arguments of this run, put there first, and of the runs handed to it arrive.</param>
    internal AppLoop(AppState appState, ListStart start, Channel<RunArguments> inbox)
    {
        _appState = appState;
        _start = start;
        _inbox = inbox;
    }

    /// <summary>Starts what the app runs from its start until it shuts down.</summary>
    public void Start()
    {
        AppStart.Start(_appState, _start, _inbox);
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
