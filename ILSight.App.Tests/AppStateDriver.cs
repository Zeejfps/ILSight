using ILSight.App.Tests.Fixtures;
using ILSight.Core;
using ILSight.ExportArea;
using ILSight.CommandArea;
using ILSight.AppSettingArea;
using ILSight.ILSightAppArea;
using ILSight.SettingArea;
using ILSight.AssemblyListArea;
using static ILSight.Analysis;
using static ILSight.Navigation;
using static ILSight.SettingArea.Settings;

namespace ILSight.App.Tests;

/// <summary>A <see cref="AppState"/> and, unless made without one, the screen over it, whose posted
/// actions queue up until the test pumps them, so a test decides what the user does before background
/// results land. A frame's steps around drawing run only when the test pumps or asks for
/// <see cref="NextFrame"/>; reading the driver changes nothing. It speaks English unless its settings
/// name a locale.</summary>
internal sealed class AppStateDriver
{
    public const string FixturesAssembly = "ILSight.App.Tests";

    /// <summary>The most virtual time a test lets pass for what moves or waits on the clock to finish, as a
    /// search's debounce or a diagram's cards gliding into place.</summary>
    public const double SettleSeconds = 10;

    /// <summary>The most real time a test waits for the UI to be ready or a condition to hold.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    public const string FixturesNamespace = "ILSight.App.Tests.Fixtures";

    private readonly PostedActions _posted = PostedActions.OfThisTest();
    private readonly UiState? _ui;

    /// <param name="settings">The settings the app starts with.</param>
    /// <param name="nuGetRequests">Sends the "open from NuGet" dialog's requests; the app's shared connections when null.</param>
    public AppStateDriver(AppSettings? settings = null, HttpMessageHandler? nuGetRequests = null)
        : this(NoProblem(settings), CommandLines.Parse([]).Arguments, withScreen: true, nuGetRequests)
    {
    }

    private AppStateDriver(SettingsLoadResult loadResult, RunArguments arguments, bool withScreen, HttpMessageHandler? nuGetRequests = null)
    {
        Step = Pump;
        loadResult.Settings.Locale ??= Locale.En;
        AppState = AppRoot.RestoreAppState(loadResult, arguments, TestSession.Shared);
        if (withScreen)
        {
            _ui = AppRoot.CreateScreen(AppState, nuGetRequests);
        }

        Commands.StartRestoringAnalyses(AppState, Analysis.BeginRestoring(AppState.Analyzer, AppState.Assemblies));
    }

    private static SettingsLoadResult NoProblem(AppSettings? settings)
    {
        return new SettingsLoadResult(settings ?? new AppSettings(), new SettingsProblem.None());
    }

    /// <summary>A driver over the app state and screen the app starts with, from <paramref name="loadResult"/>
    /// and <paramref name="arguments"/>, before the app opens any assemblies.</summary>
    public static AppStateDriver Started(SettingsLoadResult loadResult, RunArguments arguments)
    {
        return new AppStateDriver(loadResult, arguments, withScreen: true);
    }

    /// <summary>What the app starts as it starts with no files asked for: opening the active list
    /// (<see cref="AppFrames.OpenStartupAssembliesAsync"/>).</summary>
    public static Task<TabLoads> OpenAtStartupAsync(AppState appState, CancellationToken ct)
    {
        return AppFrames.OpenStartupAssembliesAsync(appState, new FilesToOpen([], null), ct);
    }

    /// <summary>A driver that has opened the fixture assembly.</summary>
    public static AppStateDriver Opened(AppSettings? settings = null)
    {
        var driver = new AppStateDriver(settings);
        driver.OpenFixtures();
        return driver;
    }

    /// <summary>A driver with an app state and no screen, which shows that the features need none.</summary>
    public static AppStateDriver WithoutScreen(AppSettings? settings = null)
    {
        return new AppStateDriver(NoProblem(settings), CommandLines.Parse([]).Arguments, withScreen: false);
    }

    /// <summary>A driver with no screen that has opened the fixture assembly.</summary>
    public static AppStateDriver OpenedWithoutScreen()
    {
        var driver = WithoutScreen();
        driver.OpenFixtures();
        return driver;
    }

    public AppState AppState { get; }

    /// <summary>The screen over <see cref="AppState"/>; a driver made without one has none to give.</summary>
    public UiState Ui => _ui ?? throw new InvalidOperationException("This driver has no screen.");

    /// <summary>The time the driver's autosave goes by, in seconds.</summary>
    public double Now { get; set; }

    /// <summary>What the last <see cref="Pump"/>'s autosave came to. A frame asks to come back when a write waits or
    /// failed; the driver's time moves only when the test moves it, so the test reads it here.</summary>
    public Saving Autosaved { get; private set; } = new Saving.Unchanged();

    public static string FixturePath => typeof(Greeter).Assembly.Location;

    /// <summary>Has <paramref name="wake"/> called whenever something is posted to the test's queue.</summary>
    public void WakeOnPost(Action wake)
    {
        _posted.WakeOnPost(wake);
    }

    /// <summary>Runs what was posted, then what a frame does around drawing when there is a screen,
    /// then the autosave, keeping what it came to (<see cref="Autosaved"/>).</summary>
    public void Pump()
    {
        _posted.Run();
        if (_ui is not null)
        {
            NextFrame();
        }

        Autosaved = AppState.SettingsSaving.SaveIfChanged(AppState, Now);
    }

    /// <summary>Does what a frame does before and after it draws: the screen follows the trees and the
    /// tabs and runs the commands the features queued for it, then tells the tabs where their views stand and
    /// hands over what the settings keep of it, the work it asked for while drawing starts, and the app runs what
    /// the screen asked of the app state.</summary>
    public void NextFrame()
    {
        AppFrames.StartFrame(Ui, AppState);
        AppFrames.KeepScreenChanges(Ui, AppState);
        AppFrames.StartAskedWork(Ui, AppState);
        QueuedCommands.RunAppCommands(Ui, AppState);
    }

    /// <summary>What a step of waiting does: a pump, or for a driver under an <see cref="AppDriver"/>, frames, which
    /// pump it first and run what work started in a frame posted back to the frame's own queue.</summary>
    public Action Step;

    /// <summary>Steps until <paramref name="done"/>.</summary>
    public void RunUntil(Func<bool> done)
    {
        Poll.Until(done, Step);
    }

    /// <summary>Has what is picked and followed shown in the active tab, as the display option does.</summary>
    public void TurnOffPreviewTabs()
    {
        if (_ui is not null)
        {
            _ui.Display = _ui.Display with { PreviewTabs = false };
        }

        AppState.Navigation.UsePreviewTabs(false);
    }

    /// <summary>Pumps until no background work the app state knows of is left.</summary>
    public void Settle()
    {
        RunUntil(IsSettled);
    }

    private bool IsSettled()
    {
        var appState = AppState;
        return appState.Work.Outstanding.Count == 0 && !appState.IsReloading && !appState.IsReopening && !appState.IsFindingDocuments && !appState.IsFindingAnalyses &&!appState.IsResolving && !appState.IsImporting && !appState.IsExporting && !appState.IsDiagramming && !appState.IsComparing && !IsSearching() && appState.Navigation.OpenTabs.All(static t => !t.Load.IsBusy) && _posted.IsEmpty;
    }

    private bool IsSearching()
    {
        if (_ui is null)
        {
            return false;
        }

        return _ui.Search.IsBusy;
    }

    /// <summary>The commands the features queued for the view since the last frame ran them, oldest first, taken
    /// off the queue as the screen would.</summary>
    public List<ViewCommand> TakeViewCommands()
    {
        var commands = AppState.ViewCommands.ToList();
        AppState.ViewCommands.Clear();
        return commands;
    }

    /// <summary>Opens the fixture assembly and waits until its root shows.</summary>
    public LoadedModule OpenFixtures()
    {
        Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(FixturePath)], ct));
        RunUntil(() => AppState.Navigation.ActiveTab?.Node?.Icon == NodeIcon.Assembly);
        Settle();
        return Assert.IsType<LoadedModule>(AppState.Navigation.ActiveTab?.Node?.Assembly);
    }

    /// <summary>The listed node at the labels under the fixture assembly's root.</summary>
    public BrowseNode Node(params ReadOnlySpan<string> labels)
    {
        var fixtures = AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == FixturePath);
        return fixtures.Root.Child(labels);
    }

    public BrowseNode Fixture(string type)
    {
        return Node(FixturesNamespace, type);
    }

    public void Show(BrowseNode node, bool inNewTab = false)
    {
        StartShowing(node, inNewTab);
        Settle();
    }

    /// <summary>Shows <paramref name="node"/> as the views ask once the frame is drawn: points a tab at it, then
    /// starts decompiling it there, without waiting for that.</summary>
    public void StartShowing(BrowseNode node, bool inNewTab = false)
    {
        Commands.StartDecompiling(AppState, AppState.Navigation.ShowNode(node, AppState.Decompiler.Setup, inNewTab));
    }

    /// <summary>Switches to the list named <paramref name="name"/> as the lists dialog and the toolbar's list menu do: in
    /// the frame, then the frame's edge starts opening it, without waiting for that.</summary>
    public void SwitchList(string name)
    {
        Commands.OpenListLater(Ui, AssemblyLists.SwitchAssemblyList(AppState.Lists, AppState.ListSession, AppState.View, name));
        NextFrame();
    }

    /// <summary>Deletes the list named <paramref name="name"/> as the delete dialog does: in the frame, then the frame's
    /// edge starts opening the list that takes its place when it was open, without waiting for that.</summary>
    public void DeleteList(string name)
    {
        Commands.OpenListLater(Ui, AssemblyLists.DeleteAssemblyList(AppState.Lists, AppState.ListSession, AppState.View, name));
        NextFrame();
    }

    /// <summary>Goes to the history entry at <paramref name="index"/> as the toolbar does: in place, then starts the
    /// load or the lookup it left, as the app's commands start them, without waiting for them.</summary>
    public void GoToEntry(int index)
    {
        var move = AppState.Navigation.GoToEntry(index, AppState.Assemblies, AppState.FinishedExports, AppState.Decompiler.Setup);
        if (move is null)
        {
            return;
        }

        Commands.StartMoving(AppState, move);
    }

    /// <summary>Goes to the history entry before the current one, as <see cref="GoToEntry"/> does.</summary>
    public void GoBack()
    {
        GoToEntry(AppState.Navigation.HistoryIndex - 1);
    }

    /// <summary>Goes to the history entry after the current one, as <see cref="GoToEntry"/> does.</summary>
    public void GoForward()
    {
        GoToEntry(AppState.Navigation.HistoryIndex + 1);
    }

    /// <summary>Starts <paramref name="workflow"/> over the app state now, as the app starts the workflows the screen
    /// asks for: it runs up to its first await before this returns, and is counted in the background work, which
    /// <see cref="Settle"/> waits out, until it ends.</summary>
    public void Start(Func<AppState, CancellationToken, Task> workflow)
    {
        var appState = AppState;
        appState.Workflows.Start(nameof(Start), ct => workflow(appState, ct));
    }

    /// <summary>As <see cref="Start(Func{AppState, CancellationToken, Task})"/>, for a workflow that points a tab at
    /// what it is to show: the load it returns is started once it ends, as the app's commands start it.</summary>
    public void Start(Func<AppState, CancellationToken, Task<DecompileTicket?>> show)
    {
        var appState = AppState;
        Commands.StartShowing(appState, nameof(Start), ct => show(appState, ct));
    }

    /// <summary>As <see cref="Start(Func{AppState, CancellationToken, Task{DecompileTicket?}})"/>, for a workflow that
    /// points several tabs.</summary>
    public void Start(Func<AppState, CancellationToken, Task<List<DecompileTicket>>> show)
    {
        var appState = AppState;
        Commands.StartShowing(appState, nameof(Start), ct => show(appState, ct));
    }

    /// <summary>As <see cref="Start(Func{AppState, CancellationToken, Task{DecompileTicket?}})"/>, for a workflow that
    /// returns the loads it started in tabs of every kind, each started once it ends, as the app's commands start them.</summary>
    public void Start(Func<AppState, CancellationToken, Task<TabLoads>> show)
    {
        var appState = AppState;
        Commands.StartShowing(appState, nameof(Start), ct => show(appState, ct));
    }

    /// <summary>Does <paramref name="change"/> to the app state now, as a command the views queue does once the frame
    /// is drawn, then starts the loads it returns, as the app's commands start them.</summary>
    public void Change(Func<AppState, TabLoads> change)
    {
        Commands.StartLoads(AppState, change(AppState));
    }

    /// <summary>Starts running <paramref name="run"/>, the comparison a change started, if any, as the app's commands
    /// start it once the frame is drawn.</summary>
    public void StartComparing(ComparisonRun? run)
    {
        if (run is null)
        {
            return;
        }

        Commands.StartComparing(AppState, run);
    }

    /// <summary>Analyzes <paramref name="node"/> as the views do.</summary>
    public void Analyze(BrowseNode node)
    {
        AnalyzerPanelView.Analyze(Ui, AppState, node);
    }

    /// <summary>Analyzes what <paramref name="reference"/> points at as the views do.</summary>
    public void Analyze(CodeReference.Navigable reference)
    {
        Commands.StartAnalyzing(AppState, AnalyzerPanelView.Analyze(Ui, AppState.Analyzer, reference));
    }

    /// <summary>What the disk says about the folder and key file the export dialog names, once the frame edge has
    /// looked at them.</summary>
    public ExportFileFacts CheckExportFiles()
    {
        LookAtExportFiles();
        var setup = Ui.ExportSetup;
        var facts = setup.Files.FindFacts(setup.Folder, setup.StrongNameKeyFile);
        if (facts is null)
        {
            throw new InvalidOperationException("The frame edge looks at what an open export dialog names.");
        }

        return facts.Value;
    }

    /// <summary>Presses Export in the export dialog once the frame edge has looked at what it names on the disk, as the
    /// command the button queues does.</summary>
    public void StartExport()
    {
        LookAtExportFiles();
        ExportDialogView.Start(Ui.ExportSetup, AppState);
    }

    private void LookAtExportFiles()
    {
        NextFrame();
        Settle();
    }

    /// <summary>Opens the export dialog on the open assemblies, with <paramref name="picked"/> ticked.</summary>
    public void OpenExportSetup(LoadedModule? picked)
    {
        Exports.OpenExportSetup(Ui.ExportSetup, AppState.Assemblies.Assemblies, picked);
    }
}
