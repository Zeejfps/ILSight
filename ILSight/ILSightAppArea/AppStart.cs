using System.Threading.Channels;
using ILSight.AssemblyListArea;
using ILSight.CommandArea;

namespace ILSight.ILSightAppArea;

/// <summary>The app's start, the one function every host that runs the whole app calls once it is built: the window's
/// startup hook after its own setup, and the test hosts. Building starts nothing; this starts what the app runs from its
/// start until it shuts down.</summary>
[Edge]
public static class AppStart
{
    /// <summary>Takes the run arguments already waiting in <paramref name="inbox"/>, this run's first, then starts the
    /// reading the root recorded as <paramref name="start"/>, the loop that takes the arguments of later runs, the watch
    /// of the user's theme folders, and the look for a newer version, unless the user turned it off.</summary>
    /// <param name="appState">The app state the root built, its start recorded on it.</param>
    /// <param name="start">The start the root recorded on <paramref name="appState"/>.</param>
    /// <param name="inbox">Where the arguments of this run, put there first, and of the runs handed to it arrive.</param>
    public static void Start(AppState appState, ListStart start, Channel<RunArguments> inbox)
    {
        // The run arguments decide from the list as on its way (a switch remembers it, a clear drops its files on their
        // way), so their handler's prerequisite is the start recorded: the root restored the state with the active list's
        // reopening begun and nothing read. Taken here, before the reading starts, a switch or a clear stops that
        // reading before it reads a file of the list it leaves.
        while (inbox.Reader.TryRead(out var arguments))
        {
            TakeArguments(appState, arguments);
        }

        StartOpeningStartupList(appState, start);
        appState.Workflows.StartUntilShutdown(ct => TakeLaterRunsAsync(appState, inbox, ct));
        WatchUserThemes(appState);
        CheckForUpdates(appState);
    }

    // None waits for another. A switch or a clear since stopped them: they read nothing then.
    private static void StartOpeningStartupList(AppState appState, ListStart start)
    {
        Commands.StartShowing(appState, nameof(AppFrames.OpenStartupAssembliesAsync),
            ct => AppFrames.OpenStartupAssembliesAsync(appState, start.Reopening, ct));
        Commands.StartRestoringAnalyses(appState, start.Analyses);
    }

    // Started on the UI thread, so it carries on there, and each one arriving wakes the app. The work each asks for is
    // a workflow of its own, so arguments that fail are reported by the workflow runner rather than ending this loop,
    // which would leave the pipe answering later runs with no one to follow them.
    private static async Task TakeLaterRunsAsync(AppState appState, Channel<RunArguments> inbox, CancellationToken ct)
    {
        await foreach (var arguments in inbox.Reader.ReadAllAsync(ct))
        {
            TakeArguments(appState, arguments);
            appState.FrontAsked = true;
        }
    }

    // The one path a run's arguments take, this run's and those handed over alike.
    private static void TakeArguments(AppState appState, RunArguments arguments)
    {
        ReportIgnored(appState, arguments);
        ApplyArguments(appState, arguments);
    }

    // Each change is made here, in this order, and the work each hands back starts at once as a workflow of its own:
    // the list's assemblies opening and the finding of its analyses, the lookup of the tabs clearing the list left
    // waiting, and opening the files. None waits for another: the tabs left waiting say what is gone while the files
    // open.
    private static void ApplyArguments(AppState appState, RunArguments arguments)
    {
        if (arguments.Language is not null)
        {
            appState.Decompiler.UseLanguage(arguments.Language.Value);
        }

        if (arguments.AssemblyList is not null)
        {
            Commands.StartOpeningList(appState, AssemblyLists.SwitchToListNamed(appState.Lists, appState.ListSession, appState.View, arguments.AssemblyList));
        }

        if (arguments.ClearList)
        {
            Commands.CloseAllAssemblies(appState);
        }

        var files = arguments.Files;
        Commands.StartShowing(appState, nameof(RunArgumentsActions.OpenRequestedFilesAsync), ct => RunArgumentsActions.OpenRequestedFilesAsync(appState, files, ct));
    }

    private static void ReportIgnored(AppState appState, RunArguments arguments)
    {
        var ignored = RunArgumentsActions.FindIgnored(arguments);
        if (ignored is null)
        {
            return;
        }

        appState.Refusals.Show(ignored);
    }

    private static void WatchUserThemes(AppState appState)
    {
        appState.Workflows.StartUntilShutdown(appState.UserThemes.WatchAsync);
        appState.Workflows.StartUntilShutdown(appState.UserIconThemes.WatchAsync);
    }

    private static void CheckForUpdates(AppState appState)
    {
        if (!appState.IsUpdateCheckAutomatic)
        {
            return;
        }

        var updates = appState.Updates;
        var check = updates.StartCheck(asked: false);
        if (check is null)
        {
            return;
        }

        StartChecking(appState.Workflows, updates, check);
    }

    // A method of its own, so that the calls that start nothing allocate no closure.
    private static void StartChecking(WorkflowRunner workflows, AppUpdates updates, UpdateCheck check)
    {
        workflows.Start(nameof(AppUpdates.CheckAsync), ct => updates.CheckAsync(check, ct));
    }
}
