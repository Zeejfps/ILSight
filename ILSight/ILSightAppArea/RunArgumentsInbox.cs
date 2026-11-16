using System.Threading.Channels;
using ILSight.AssemblyListArea;
using ILSight.CommandArea;

namespace ILSight.ILSightAppArea;

/// <summary>The inbox of run arguments, this run's, put there first, and those later runs hand the window already open:
/// each applied down one path, the ignored ones told on the refusal line, then each change they ask for made to the app
/// state.</summary>
[Edge]
public sealed class RunArgumentsInbox
{
    private readonly AppState _appState;
    private readonly Channel<RunArguments> _inbox;

    /// <param name="appState">The app state the arguments change.</param>
    /// <param name="inbox">Where the arguments of this run, put there first, and of the runs handed to it arrive.</param>
    internal RunArgumentsInbox(AppState appState, Channel<RunArguments> inbox)
    {
        _appState = appState;
        _inbox = inbox;
    }

    /// <summary>Applies the arguments already waiting, this run's first.</summary>
    public void ApplyWaiting()
    {
        while (_inbox.Reader.TryRead(out var arguments))
        {
            Apply(arguments);
        }
    }

    /// <summary>Starts applying the arguments of later runs as they arrive, each bringing the window to the front, until
    /// the app shuts down.</summary>
    public void StartApplyingLaterRuns()
    {
        _appState.Workflows.StartUntilShutdown(ApplyLaterRunsAsync);
    }

    // Started on the UI thread, so it carries on there, and each one arriving wakes the app. The work each asks for is
    // a workflow of its own, so arguments that fail are reported by the workflow runner rather than ending this loop,
    // which would leave the pipe answering later runs with no one to apply them.
    private async Task ApplyLaterRunsAsync(CancellationToken ct)
    {
        await foreach (var arguments in _inbox.Reader.ReadAllAsync(ct))
        {
            Apply(arguments);
            _appState.FrontAsked = true;
        }
    }

    // The one path a run's arguments take, this run's and those handed over alike.
    private void Apply(RunArguments arguments)
    {
        ReportIgnored(arguments);
        MakeChanges(arguments);
    }

    // Each change is made here, in this order, and the work each hands back starts at once as a workflow of its own:
    // the list's assemblies opening and the finding of its analyses, the lookup of the tabs clearing the list left
    // waiting, and opening the files. None waits for another: the tabs left waiting say what is gone while the files
    // open.
    private void MakeChanges(RunArguments arguments)
    {
        var appState = _appState;
        if (arguments.Language is not null)
        {
            appState.Decompiler.UseLanguage(arguments.Language.Value);
        }

        if (arguments.AssemblyList is not null)
        {
            Commands.StartOpeningList(appState, AssemblyLists.SwitchToListNamed(appState.Lists, appState.ListSession, appState.KeptScreen.View, arguments.AssemblyList));
        }

        if (arguments.ClearList)
        {
            Commands.CloseAllAssemblies(appState);
        }

        var files = arguments.Files;
        var work = appState.ListSession.Assemblies.Work;
        Commands.StartShowingSettled(appState, nameof(RunArgumentsActions.OpenRequestedFilesAsync), ct => RunArgumentsActions.OpenRequestedFilesAsync(appState, files, work, ct),
            () => appState.ListSession.Navigation.FailOpening(), work.Ending);
    }

    private void ReportIgnored(RunArguments arguments)
    {
        var ignored = RunArgumentsActions.FindIgnored(arguments);
        if (ignored is null)
        {
            return;
        }

        _appState.Refusals.Show(ignored);
    }
}
