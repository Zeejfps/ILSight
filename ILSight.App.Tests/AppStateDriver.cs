using ZGF.Gui.Immediate;
using ILSight.App.Tests.Fixtures;
using ILSight.Core;
using ILSight.ExportArea;
using ILSight.CommandArea;
using ILSight.AppSettingArea;
using ILSight.ILSightAppArea;
using ILSight.SettingArea;
using ILSight.AssemblyListArea;
using ILSight.FrameworkArea;
using ILSight.ProcessArea;
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

    private readonly PostedActions _posted;
    private readonly UiState? _ui;

    /// <summary>A driver over the app state and screen the root restores (<see cref="AppRoot.RestoreApp"/>) from
    /// <paramref name="settings"/>, with no window, which has started what the app starts as it starts with no
    /// arguments, the active list's assemblies opening and its saved analyses being found, and waited for the list to
    /// open (<see cref="WaitForTheStart"/>).</summary>
    /// <param name="settings">The settings the app starts with.</param>
    /// <param name="nuGetRequests">Sends the "open from NuGet" dialog's requests; the app's shared connections when null.</param>
    /// <param name="runtimeScan">Scans the runtime for the "open from framework" dialog; the installed runtime's when null.</param>
    /// <param name="processes">Lists the running processes for the "open from process" dialog; this machine's when null.</param>
    public AppStateDriver(AppSettings? settings = null, HttpMessageHandler? nuGetRequests = null, FrameworkBrowser.IPorts? runtimeScan = null,
        ProcessBrowser.IPorts? processes = null)
        : this(PostedActions.OfThisTest(), AppRoot.RestoreApp(NoProblem(settings), TestSession.Shared, nuGetRequests, runtimeScan, processes))
    {
        WaitForTheStart();
    }

    /// <summary>A driver over <paramref name="restored"/>, which starts what the root recorded for the list it starts
    /// with, as the app does once it has taken its arguments (none here).</summary>
    private AppStateDriver(PostedActions posted, RestoredApp restored)
        : this(posted, restored.AppState, restored.Screen)
    {
        using var turn = _posted.Install();
        AppFrames.StartOpeningStartupList(AppState, restored.Start);
    }

    /// <summary>A driver over <paramref name="appState"/> and <paramref name="ui"/>, the screen over it, if any, which
    /// <paramref name="posted"/> took the test's posts for before the root built them.</summary>
    private AppStateDriver(PostedActions posted, AppState appState, UiState? ui)
    {
        _posted = posted;
        Step = Pump;
        AppState = appState;
        _ui = ui;
    }

    /// <summary>What reading <paramref name="settings"/> gave, with nothing wrong (new settings when null), speaking
    /// English unless they name a locale.</summary>
    public static SettingsLoadResult NoProblem(AppSettings? settings)
    {
        var loadResult = new SettingsLoadResult
        {
            Settings = settings ?? new AppSettings(),
            Problem = new SettingsProblem.None(),
        };
        SpeakEnglishUnlessNamed(loadResult);
        return loadResult;
    }

    /// <summary>Has the settings in <paramref name="loadResult"/> speak English unless they name a locale.</summary>
    private static void SpeakEnglishUnlessNamed(SettingsLoadResult loadResult)
    {
        loadResult.Settings.Locale ??= Locale.En;
    }

    /// <summary>A driver over the app state and screen the app starts with from <paramref name="loadResult"/>, which
    /// has started what the app starts as it starts with no arguments and waited for the list to open, as
    /// <see cref="AppStateDriver(AppSettings?, HttpMessageHandler?, FrameworkBrowser.IPorts?, ProcessBrowser.IPorts?)"/>
    /// does.</summary>
    public static AppStateDriver Started(SettingsLoadResult loadResult)
    {
        var posted = PostedActions.OfThisTest();
        SpeakEnglishUnlessNamed(loadResult);
        var driver = new AppStateDriver(posted, AppRoot.RestoreApp(loadResult, TestSession.Shared));
        driver.WaitForTheStart();
        return driver;
    }

    /// <summary>A driver as <see cref="AppStateDriver(AppSettings?, HttpMessageHandler?, FrameworkBrowser.IPorts?,
    /// ProcessBrowser.IPorts?)"/> makes it, returned while the list it starts with is still opening, for a test of
    /// what happens meanwhile.</summary>
    public static AppStateDriver Starting(AppSettings? settings = null)
    {
        return new AppStateDriver(PostedActions.OfThisTest(), AppRoot.RestoreApp(NoProblem(settings), TestSession.Shared));
    }

    /// <summary>Runs what was posted, and nothing else a frame does, until the reload the app's start began (or the
    /// one its arguments switched to) has ended: a test then begins from an app whose list is open, whatever the thread
    /// pool's pace.</summary>
    public void WaitForTheStart()
    {
        Poll.Until(() => !AppState.IsReloading, _posted.Run);
    }

    /// <summary>A driver over <paramref name="restored"/>, the app state and screen an <see cref="AppDriver"/> had the
    /// root build with its app, which <paramref name="posted"/> took the test's posts for before. The app starts what
    /// the root recorded, once it has taken its arguments (<see cref="ILSightApp.StartTakingArguments"/>).</summary>
    public static AppStateDriver Over(PostedActions posted, RestoredApp restored)
    {
        return new AppStateDriver(posted, restored.AppState, restored.Screen);
    }

    /// <summary>A driver that has opened the fixture assembly.</summary>
    public static AppStateDriver Opened(AppSettings? settings = null)
    {
        var driver = new AppStateDriver(settings);
        driver.OpenFixtures();
        return driver;
    }

    /// <summary>A driver with an app state and no screen, which shows that the features need none. It starts restoring
    /// the analyses the settings kept, and opens no list.</summary>
    public static AppStateDriver WithoutScreen(AppSettings? settings = null)
    {
        var posted = PostedActions.OfThisTest();
        var appState = AppRoot.RestoreStartingState(NoProblem(settings), TestSession.Shared);
        var driver = new AppStateDriver(posted, appState, ui: null);
        using (driver.Turn())
        {
            Commands.StartRestoringAnalyses(appState, Analysis.BeginRestoring(appState.Analyzer, appState.Assemblies));
        }

        return driver;
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

    /// <summary>Installs the test's UI context for a turn of the app's code run outside <see cref="Pump"/>, as the app's
    /// loop installs it for its own turns, until the returned scope is disposed.</summary>
    public UiSynchronizationContext.Scope Turn()
    {
        return _posted.Install();
    }

    /// <summary>Does <paramref name="change"/>, the app's code a test calls itself, in a turn of the driver's loop, as the
    /// app's edge runs it: what it starts comes back to the loop, which the test's pumps and frames run.</summary>
    public void InTurn(Action change)
    {
        using var turn = _posted.Install();
        change();
    }

    /// <summary>Runs <paramref name="work"/>, the app's work a test starts itself (a use case, a handler), as the app's
    /// edge starts it: begun in a turn of the driver's loop, each wait comes back to the loop, whose posts the driver
    /// runs until the work ends, and nothing else a frame does; answers what it came to.</summary>
    public async Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        return await EndAsync(BeginAsync(work));
    }

    /// <summary>As <see cref="RunAsync{T}(Func{Task{T}})"/>, for work that answers nothing.</summary>
    public async Task RunAsync(Func<Task> work)
    {
        await EndAsync(BeginAsync(work));
    }

    /// <summary>Begins <paramref name="work"/> in a turn of the driver's loop and answers it under way, for a test that
    /// acts while it waits, then ends it with <see cref="EndAsync{T}(Task{T})"/>.</summary>
    public Task<T> BeginAsync<T>(Func<Task<T>> work)
    {
        using var turn = _posted.Install();
        return work();
    }

    /// <summary>As <see cref="BeginAsync{T}(Func{Task{T}})"/>, for work that answers nothing.</summary>
    public Task BeginAsync(Func<Task> work)
    {
        using var turn = _posted.Install();
        return work();
    }

    /// <summary>Runs what is posted to the driver's loop until <paramref name="work"/>, begun in a turn
    /// (<see cref="BeginAsync{T}(Func{Task{T}})"/>), has ended, then answers what it came to.</summary>
    public async Task<T> EndAsync<T>(Task<T> work)
    {
        RunPostsUntil(work);
        return await work;
    }

    /// <summary>As <see cref="EndAsync{T}(Task{T})"/>, for work that answers nothing.</summary>
    public async Task EndAsync(Task work)
    {
        RunPostsUntil(work);
        await work;
    }

    /// <summary>Runs what was posted, each run a turn, until <paramref name="work"/> has ended; nothing when it has.</summary>
    private void RunPostsUntil(Task work)
    {
        if (work.IsCompleted)
        {
            return;
        }

        Poll.Until(() => work.IsCompleted, _posted.Run);
    }

    /// <summary>Has <paramref name="wake"/> called whenever something is posted to the test's queue.</summary>
    public void WakeOnPost(Action wake)
    {
        _posted.WakeOnPost(wake);
    }

    /// <summary>Runs what was posted, then what a frame does around drawing when there is a screen,
    /// then the autosave, keeping what it came to (<see cref="Autosaved"/>).</summary>
    public void Pump()
    {
        using var turn = _posted.Install();
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
        using var turn = _posted.Install();
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
        return appState.Work.Outstanding.Count == 0 && !appState.IsReloading && !appState.IsReopening && !appState.IsResolving && !appState.Export.IsRunning
            && !IsSearching() && appState.Navigation.OpenTabs.All(IsShownWhole) && _posted.IsEmpty;
    }

    /// <summary>Whether <paramref name="tab"/> shows nothing on its way, as it draws it: no code loading, no diagram
    /// reading cards or laying them out, no comparison running.</summary>
    private static bool IsShownWhole(DocumentTab tab)
    {
        if (tab.Load.IsBusy)
        {
            return false;
        }

        var diagram = tab.Diagram;
        if (diagram is not null && diagram.IsBusy)
        {
            return false;
        }

        var comparison = tab.Comparison;
        return comparison is null || !comparison.IsComparing;
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
    public async Task<BrowseNode> NodeAsync(params string[] labels)
    {
        var fixtures = AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == FixturePath);
        return await fixtures.ReadRootAsync().GetChildAsync(labels);
    }

    public async Task<BrowseNode> FixtureAsync(string type)
    {
        return await NodeAsync(FixturesNamespace, type);
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
        using var turn = _posted.Install();
        Commands.StartDecompiling(AppState, AppState.Navigation.ShowNode(node, AppState.Decompiler.Setup, inNewTab));
    }

    /// <summary>Switches to the list named <paramref name="name"/> as the lists dialog and the toolbar's list menu do: in
    /// the frame, then the frame's edge starts opening it, without waiting for that.</summary>
    public void SwitchList(string name)
    {
        using var turn = _posted.Install();
        Commands.OpenListLater(Ui, AssemblyLists.SwitchAssemblyList(AppState.Lists, AppState.ListSession, AppState.View, name));
        NextFrame();
    }

    /// <summary>Deletes the list named <paramref name="name"/> as the delete dialog does: in the frame, then the frame's
    /// edge starts opening the list that takes its place when it was open, without waiting for that.</summary>
    public void DeleteList(string name)
    {
        using var turn = _posted.Install();
        Commands.OpenListLater(Ui, AssemblyLists.DeleteAssemblyList(AppState.Lists, AppState.ListSession, AppState.View, name));
        NextFrame();
    }

    /// <summary>Goes to the history entry at <paramref name="index"/> as the toolbar does: in place, then starts the
    /// load or the lookup it left, as the app's commands start them, without waiting for them.</summary>
    public void GoToEntry(int index)
    {
        StartMoving(AppState.Navigation.GoToEntry(index, AppState.Assemblies, AppState.FinishedExports, AppState.Decompiler.Setup));
    }

    /// <summary>Goes back as the toolbar's Back does, passing over the entries that can't be shown, then starts what
    /// it left as <see cref="GoToEntry"/> does.</summary>
    public void GoBack()
    {
        StartMoving(AppState.Navigation.GoBack(AppState.Assemblies, AppState.FinishedExports, AppState.Decompiler.Setup));
    }

    /// <summary>Goes forward as <see cref="GoBack"/> goes back.</summary>
    public void GoForward()
    {
        StartMoving(AppState.Navigation.GoForward(AppState.Assemblies, AppState.FinishedExports, AppState.Decompiler.Setup));
    }

    private void StartMoving(HistoryMove? move)
    {
        using var turn = _posted.Install();
        if (move is null)
        {
            return;
        }

        Commands.StartMoving(AppState, move);
    }

    /// <summary>Starts <paramref name="workflow"/> over the app state now, as the app starts the workflows the screen
    /// asks for: it runs up to its first await before this returns, and is counted in the background work, which
    /// <see cref="Settle"/> waits out, until it ends.</summary>
    public void Start(Func<AppState, CancellationToken, Task> workflow)
    {
        using var turn = _posted.Install();
        var appState = AppState;
        appState.Workflows.Start(nameof(Start), ct => workflow(appState, ct));
    }

    /// <summary>As <see cref="Start(Func{AppState, CancellationToken, Task})"/>, for a workflow that points a tab at
    /// what it is to show: the load it returns is started once it ends, as the app's commands start it.</summary>
    public void Start(Func<AppState, CancellationToken, Task<DecompileTicket?>> show)
    {
        using var turn = _posted.Install();
        var appState = AppState;
        Commands.StartShowing(appState, nameof(Start), ct => show(appState, ct));
    }

    /// <summary>As <see cref="Start(Func{AppState, CancellationToken, Task{DecompileTicket?}})"/>, for a workflow that
    /// points several tabs.</summary>
    public void Start(Func<AppState, CancellationToken, Task<List<DecompileTicket>>> show)
    {
        using var turn = _posted.Install();
        var appState = AppState;
        Commands.StartShowing(appState, nameof(Start), ct => show(appState, ct));
    }

    /// <summary>As <see cref="Start(Func{AppState, CancellationToken, Task{DecompileTicket?}})"/>, for a workflow that
    /// returns the loads it started in tabs of every kind, each started once it ends, as the app's commands start them.</summary>
    public void Start(Func<AppState, CancellationToken, Task<TabLoads>> show)
    {
        using var turn = _posted.Install();
        var appState = AppState;
        Commands.StartShowing(appState, nameof(Start), ct => show(appState, ct));
    }

    /// <summary>Does <paramref name="change"/> to the app state now, as a command the views queue does once the frame
    /// is drawn, then starts the loads it returns, as the app's commands start them.</summary>
    public void Change(Func<AppState, TabLoads> change)
    {
        using var turn = _posted.Install();
        Commands.StartLoads(AppState, change(AppState));
    }

    /// <summary>Starts running <paramref name="run"/>, the comparison a change started, if any, as the app's commands
    /// start it once the frame is drawn.</summary>
    public void StartComparing(ComparisonRun? run)
    {
        using var turn = _posted.Install();
        if (run is null)
        {
            return;
        }

        Commands.StartComparing(AppState, run);
    }

    /// <summary>Analyzes what <paramref name="node"/> stands for as the views do.</summary>
    public void Analyze(BrowseNode node)
    {
        using var turn = _posted.Install();
        Commands.Analyze(AppState, Ui, node.Analysis ?? throw new InvalidOperationException($"{node.Text} stands for nothing to analyze."));
    }

    /// <summary>Analyzes what <paramref name="reference"/> points at as the views do.</summary>
    public void Analyze(CodeReference.Navigable reference)
    {
        using var turn = _posted.Install();
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
        using var turn = _posted.Install();
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
        using var turn = _posted.Install();
        Exports.OpenExportSetup(Ui.ExportSetup, AppState.Assemblies.Assemblies, picked);
    }
}
