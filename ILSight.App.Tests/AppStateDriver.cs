using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Testing;
using ZGF.Gui.Testing;
using ILSight.App.Tests.Fixtures;
using ILSight.Core;
using ILSight.Localization;
using static ILSight.Analysis;
using static ILSight.Assemblies;
using static ILSight.Navigation;
using static ILSight.Settings;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

/// <summary>Actions posted from other threads, queued until the test runs them. A test's drivers
/// share one, which is also where <c>await</c> on the test's thread continues. Each app's frames are woken
/// when something is posted, so one waiting for its UI to be ready runs a frame, which runs the queue.</summary>
internal sealed class PostedActions
{
    [ThreadStatic] private static PostedActions? _ofThisTest;

    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly object _gate = new();
    private Action[] _wakes = [];
    private readonly UiSynchronizationContext _context;

    private PostedActions()
    {
        _context = new UiSynchronizationContext(Post);
    }

    public bool IsEmpty => _queue.IsEmpty;

    /// <summary>The queue of the test running on this thread. xUnit gives every test a context of
    /// its own, so a test that has not made one yet gets a new queue.</summary>
    public static PostedActions OfThisTest()
    {
        var existing = _ofThisTest;
        if (existing != null && SynchronizationContext.Current == existing._context)
            return existing;
        var posted = new PostedActions();
        SynchronizationContext.SetSynchronizationContext(posted._context);
        _ofThisTest = posted;
        return posted;
    }

    public void Post(Action action)
    {
        _queue.Enqueue(action);
        Action[] wakes;
        lock (_gate)
            wakes = _wakes;
        foreach (var wake in wakes)
            wake();
    }

    /// <summary>Has <paramref name="wake"/> called, from any thread, whenever something is posted.</summary>
    public void WakeOnPost(Action wake)
    {
        lock (_gate)
            _wakes = [.. _wakes, wake];
    }

    /// <summary>Runs what was posted so far, and what that posts.</summary>
    public void Run()
    {
        while (_queue.TryDequeue(out var action))
            action();
    }
}

internal static class Poll
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Takes <paramref name="step"/> until <paramref name="done"/>, at least once.</summary>
    public static void Until(Func<bool> done, Action step)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            step();
            if (done()) return;
            if (clock.Elapsed > Timeout) throw new TimeoutException("It did not get there in time.");
            Thread.Sleep(1);
        }
    }
}

internal static class FoundAnalyses
{
    /// <summary>The analyzer's roots whose symbols were found.</summary>
    public static List<AnalyzerSymbolNode> Found(this AnalyzerTree analyzer)
    {
        return analyzer.Roots
            .OfType<AnalyzerSymbolNode>()
            .ToList();
    }
}

/// <summary>The icons of the built-in reference icon theme, which the tests' app draws with.</summary>
internal static class BuiltInGlyphs
{
    public static AppIcons Icons { get; } = BuiltInIconThemes.Reference.Icons;

    /// <summary>The text <paramref name="glyph"/>, a font glyph, is drawn as.</summary>
    public static string Text(IconGlyph glyph) => glyph.Text!;
}

/// <summary>What the screen names new things in English.</summary>
internal static class Names
{
    public static string Bookmark(int number) => Strings.En.BookmarksDefaultName(number);

    public static string Comparison(LoadedAssembly left) => Strings.En.ComparisonTitle(left.Name);

    public static string Diagram(CodeReference type) => Strings.En.DiagramTitle(type.Name);
}

internal static class AppErrors
{
    /// <summary>The error in English words.</summary>
    public static ProblemText Words(this AppError error) => ProblemTexts.Describe(Strings.En, error.Problem);
}

internal static class NodeIconQueries
{
    extension(BrowseNode node)
    {
        /// <summary>The icon the tree draws the node with.</summary>
        public NodeIcon Icon => NodeIcons.Of(node.Subject);
    }
}

internal static class TempSettings
{
    public static AppSettings Settings(this TempDir temp)
    {
        return SettingsJson.LoadSettings(temp.PathOf("settings.json")).Settings;
    }
}

internal static class Screens
{
    /// <summary>A screen over an app state with nothing open, for drawing a widget alone.</summary>
    public static UiState Blank()
    {
        return AppFrames.CreateUiState(LoadAppState(new AppSettings { Locale = Locale.En }, TestSession.Shared));
    }
}

/// <summary>A <see cref="AppState"/> and, unless made without one, the screen over it, whose posted
/// actions queue up until the test pumps them, so a test decides what the user does before background
/// results land. A frame's steps around drawing run only when the test pumps or asks for
/// <see cref="NextFrame"/>; reading the driver changes nothing. It speaks English unless its settings
/// name a locale.</summary>
internal sealed class AppStateDriver
{
    public const string FixturesAssembly = "ILSight.App.Tests";
    public const string FixturesNamespace = "ILSight.App.Tests.Fixtures";

    private readonly PostedActions _posted = PostedActions.OfThisTest();
    private readonly UiState? _ui;

    public AppStateDriver(AppSettings? settings = null) : this(NoProblem(settings), CommandLines.Parse([]).Arguments, withScreen: true)
    {
    }

    private AppStateDriver(SettingsLoadResult loadResult, RunArguments arguments, bool withScreen)
    {
        Step = Pump;
        loadResult.Settings.Locale ??= Locale.En;
        AppState = ILSightAppLauncher.LoadAppState(loadResult, arguments, TestSession.Shared);
        if (withScreen)
            _ui = AppFrames.CreateUiState(AppState);
        RestoreAnalyses(AppState);
    }

    private static SettingsLoadResult NoProblem(AppSettings? settings) =>
        new(settings ?? new AppSettings(), new SettingsProblem.None());

    /// <summary>A driver over the app state and screen the app starts with, from <paramref name="loadResult"/>
    /// and <paramref name="arguments"/>, before the app opens any assemblies.</summary>
    public static AppStateDriver Started(SettingsLoadResult loadResult, RunArguments arguments) => new(loadResult, arguments, withScreen: true);

    /// <summary>A driver that has opened the fixture assembly.</summary>
    public static AppStateDriver Opened(AppSettings? settings = null)
    {
        var driver = new AppStateDriver(settings);
        driver.OpenFixtures();
        return driver;
    }

    /// <summary>A driver with an app state and no screen, which shows that the features need none.</summary>
    public static AppStateDriver WithoutScreen(AppSettings? settings = null) => new(NoProblem(settings), CommandLines.Parse([]).Arguments, withScreen: false);

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

    public static string FixturePath => typeof(Greeter).Assembly.Location;

    /// <summary>Runs what was posted, then what a frame does around drawing when there is a screen,
    /// then the autosave.</summary>
    /// <summary>Has <paramref name="wake"/> called whenever something is posted to the test's queue.</summary>
    public void WakeOnPost(Action wake) => _posted.WakeOnPost(wake);

    public void Pump()
    {
        _posted.Run();
        if (_ui != null)
            NextFrame();
        SaveSettingsIfChanged(AppState, Now);
    }

    /// <summary>Does what a frame does before and after it draws: the screen follows the trees and the
    /// tabs and runs the commands the features queued for it, then tells the tabs where their views stand and
    /// hands over what the settings keep of it, and the app runs what the screen asked of the app state.</summary>
    public void NextFrame()
    {
        AppFrames.StartFrame(Ui, AppState);
        AppFrames.KeepScreenChanges(Ui, AppState);
        AppFrames.RunAppCommands(Ui, AppState);
    }

    /// <summary>What a step of waiting does: a pump, or for a driver under an <see cref="AppDriver"/>, frames, which
    /// pump it first and run what work started in a frame posted back to the frame's own queue.</summary>
    public Action Step;

    /// <summary>Steps until <paramref name="done"/>.</summary>
    public void RunUntil(Func<bool> done) => Poll.Until(done, Step);

    /// <summary>Has what is picked and followed shown in the active tab, as the display option does.</summary>
    public void TurnOffPreviewTabs()
    {
        if (_ui != null)
            _ui.Display = _ui.Display with { PreviewTabs = false };
        Navigation.UsePreviewTabs(AppState, false);
    }

    /// <summary>Pumps until no background work the app state knows of is left.</summary>
    public void Settle() => RunUntil(IsSettled);

    private bool IsSettled()
    {
        var appState = AppState;
        return appState.Work.Outstanding.Count == 0 && !appState.IsReloading && !appState.IsReopening && !appState.IsFindingDocuments && !appState.IsFindingAnalyses &&!appState.IsResolving && !appState.IsImporting && !appState.IsExporting && !appState.IsDiagramming && !appState.IsComparing && !IsSearching() && appState.Tabs.Open.All(static t => !t.IsBusy) && _posted.IsEmpty;
    }

    private bool IsSearching()
    {
        if (_ui == null)
            return false;
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
        OpenAssembly(AppState, FixturePath);
        RunUntil(() => AppState.Tabs.Active?.Node?.Icon == NodeIcon.Assembly);
        Settle();
        return Assert.IsType<LoadedModule>(AppState.Tabs.Active!.Node!.Assembly);
    }

    /// <summary>The listed node at the labels under the fixture assembly's root.</summary>
    public BrowseNode Node(params ReadOnlySpan<string> labels)
    {
        var fixtures = AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == FixturePath);
        return fixtures.Root.Child(labels);
    }

    public BrowseNode Fixture(string type) => Node(FixturesNamespace, type);

    public void Show(BrowseNode node, bool inNewTab = false)
    {
        ShowNode(AppState, node, inNewTab);
        Settle();
    }

    /// <summary>Analyzes <paramref name="node"/> as the views do.</summary>
    public void Analyze(BrowseNode node) => AnalyzerPanelView.Analyze(Ui, AppState, node);

    /// <summary>Analyzes what <paramref name="reference"/> points at as the views do.</summary>
    public void Analyze(CodeReference.Navigable reference) => AnalyzerPanelView.Analyze(Ui, AppState, reference);

    /// <summary>Opens the export dialog on the open assemblies, with <paramref name="picked"/> ticked.</summary>
    public void OpenExportSetup(LoadedModule? picked)
    {
        Exports.OpenExportSetup(Ui.ExportSetup, AppState.Assemblies.Assemblies, picked);
    }
}

/// <summary>Renders the whole app window frame by frame over a <see cref="AppStateDriver"/>, which
/// it pumps before each frame.</summary>
internal sealed class AppDriver
{
    public AppDriver(float width = 1000f, float height = 700f, AppSettings? settings = null, RunArguments? queuedArguments = null, ITextMeasurer? measurer = null, Channel<RunArguments>? inbox = null)
    {
        Driver = new AppStateDriver(settings);
        inbox ??= Channel.CreateUnbounded<RunArguments>();
        if (queuedArguments != null)
            inbox.Writer.TryWrite(queuedArguments);
        App = new ILSightApp(AppState, Ui, CommandLines.Parse([]).Arguments.Files, inbox);
        Frames = UiFrames.Running(App, measurer, width: width, height: height, background: AppState.Work);
        Frames.FrameTime = 1f / 60f;
        // What the test starts outside a frame comes back through the test's queue, which wakes these frames, so
        // waiting for the UI to be ready runs the frame that pumps it. UiFrames hands out its Ui only to a frame,
        // so one empty frame, which takes no time, fetches it.
        var ui = Frames.Run(0f, static ui => ui);
        Driver.WakeOnPost(ui.RequestFrame);
        Background.StartForever(App.ApplyPendingArguments(), problem => Errors.Report(AppState, problem));
        Frames.BeforeFrame = PumpAtFrameTime;
        Driver.Step = RunFramesInPlace;
    }

    private void PumpAtFrameTime()
    {
        Driver.Now = Frames.RealTime;
        Driver.Pump();
    }

    /// <summary>An app that has opened the fixture assembly, before its first frame.</summary>
    public static AppDriver Opened(float width = 1000f, float height = 700f, ITextMeasurer? measurer = null)
    {
        var app = new AppDriver(width, height, measurer: measurer);
        app.Driver.OpenFixtures();
        return app;
    }

    public AppStateDriver Driver { get; }
    public AppState AppState => Driver.AppState;

    /// <summary>The screen the app draws.</summary>
    public UiState Ui => Driver.Ui;
    public ILSightApp App { get; }
    public UiFrames Frames { get; }
    public UiTheme Theme => Theming.ResolveTheme(AppState).Ui;

    public BrowseNode Node(params ReadOnlySpan<string> labels) => Driver.Node(labels);

    public BrowseNode Fixture(string type) => Driver.Fixture(type);

    public void Run(int frames = 1) => Frames.Frame(frames);

    /// <summary>Runs frames until <paramref name="done"/>.</summary>
    public void RunUntil(Func<bool> done) => Poll.Until(done, () => Frames.Frame());

    /// <summary>Waits until the UI is ready, with the background work done and a frame run after it, then runs
    /// <paramref name="frames"/> frames.</summary>
    public void Settle(int frames = 0)
    {
        Frames.WaitUntilReady(StepTimeout);
        Run(frames);
    }

    /// <summary>Runs frames without advancing time, each pumping the app state first, until none is asked for.</summary>
    private void RunFramesInPlace() => Frames.WaitUntil(static () => true, StepTimeout);

    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Shows <paramref name="node"/> as <see cref="AppStateDriver.Show"/>, then runs <paramref name="frames"/> frames.</summary>
    public void Show(BrowseNode node, int frames = 0, bool inNewTab = false)
    {
        Driver.Show(node, inNewTab);
        Run(frames);
    }

    public void Analyze(BrowseNode node) => Driver.Analyze(node);

    public void OpenExportSetup(LoadedModule? picked) => Driver.OpenExportSetup(picked);

    /// <summary>Where <paramref name="icon"/> was drawn on the last frame, in window coordinates.</summary>
    public UiRect TextRect(IconGlyph icon) => TextRect(BuiltInGlyphs.Text(icon));

    /// <summary>Where <paramref name="icon"/> was last drawn, as <see cref="LastTextRect(string)"/>.</summary>
    public UiRect LastTextRect(IconGlyph icon) => LastTextRect(BuiltInGlyphs.Text(icon));

    public UiFrames Click(IconGlyph icon) => Frames.Click(TextRect(icon));

    /// <summary>Clicks where <paramref name="icon"/> was last drawn, as <see cref="ClickLast(string)"/>.</summary>
    public UiFrames ClickLast(IconGlyph icon) => Frames.Click(LastTextRect(icon));

    /// <summary>Where the text was drawn on the last frame, in window coordinates.</summary>
    public UiRect TextRect(string text) => Drawn(text, Frames.Canvas.Texts.FirstOrDefault(t => t.Inputs.Text == text));

    /// <summary>Where <paramref name="text"/> was last drawn: the one furthest into the window when
    /// the same text, such as an icon, is also drawn before it.</summary>
    public UiRect LastTextRect(string text) => Drawn(text, Frames.Canvas.Texts.LastOrDefault(t => t.Inputs.Text == text));

    private UiRect Drawn(string text, RecordedText? drawn)
    {
        if (drawn == null)
            throw new InvalidOperationException($"'{text}' was not drawn: {string.Join(", ", Frames.Canvas.Texts.Select(t => t.Inputs.Text))}");
        return Frames.ToUi(drawn.Inputs.Position);
    }

    /// <summary>Where the tab titled <paramref name="title"/> was drawn on the last frame; the first of
    /// those drawn when several tabs are.</summary>
    public UiRect TabTitleRect(string title) => ListTabTitleRects(title)[0];

    /// <summary>Where the tab titled <paramref name="title"/> was drawn on the last frame; the last of
    /// those drawn when several tabs are.</summary>
    public UiRect LastTabTitleRect(string title)
    {
        var rects = ListTabTitleRects(title);
        return rects[rects.Count - 1];
    }

    /// <summary>The texts reading <paramref name="title"/> drawn highest, in the order drawn: tab titles,
    /// over the omnibar and the tree that name the same node.</summary>
    private List<UiRect> ListTabTitleRects(string title)
    {
        var rects = Frames.TextRects()
            .Where(t => t.Text == title)
            .Select(t => t.Rect)
            .ToList();
        if (rects.Count == 0)
            throw new InvalidOperationException($"'{title}' was not drawn.");
        var top = rects.Min(r => r.Y);
        return rects
            .Where(r => r.Y < top + 0.5f)
            .ToList();
    }

    /// <summary>The texts drawn in the status bar, the window's bottom row.</summary>
    public IEnumerable<UiRect> StatusTexts()
    {
        var top = StatusBarTop;
        return Frames.TextRects()
            .Where(t => t.Rect.Y >= top)
            .Select(t => t.Rect);
    }

    /// <summary>Where <paramref name="text"/> was drawn in the status bar on the last frame.</summary>
    public UiRect StatusText(string text)
    {
        var top = StatusBarTop;
        var (drawn, rect) = Frames.TextRects().LastOrDefault(t => t.Text == text && t.Rect.Y >= top);
        if (drawn == null)
            throw new InvalidOperationException($"'{text}' was not drawn in the status bar.");
        return rect;
    }

    private float StatusBarTop => Frames.Height - Theme.StatusBarHeight - 0.5f;

    public UiFrames MoveTo(string text) => Frames.MoveTo(Selector.WithText(text)).Frame();

    public UiFrames Click(string text) => Frames.ClickOn(Selector.WithText(text));

    /// <summary>Clicks the first text drawn starting with <paramref name="prefix"/>, as a label cut
    /// short with an ellipsis is.</summary>
    public UiFrames ClickStartingWith(string prefix)
    {
        var drawn = Frames.Canvas.Texts.FirstOrDefault(t => t.Inputs.Text.StartsWith(prefix, StringComparison.Ordinal));
        return Frames.Click(Drawn(prefix, drawn));
    }

    /// <summary>Clicks the last drawn <paramref name="text"/>: the one in the topmost open menu
    /// when the same text is also on the window below it.</summary>
    public UiFrames ClickMenuItem(string text) => Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, text));

    /// <summary>Right-clicks the tree row drawn as <paramref name="row"/> and picks <paramref name="item"/>
    /// from its menu.</summary>
    public void PickFromTreeMenu(string row, string item)
    {
        Frames.ClickOn(Selector.OfRole(SemanticRole.TreeItem, row), PointerButton.Right).Frame();
        ClickMenuItem(item).Frame();
    }

    /// <summary>Clicks where <paramref name="text"/> was last drawn, such as an icon that the assemblies
    /// panel's toolbar also draws.</summary>
    public UiFrames ClickLast(string text) => Frames.Click(LastTextRect(text));

    public UiFrames DoubleClick(string text) => DoubleClickOn(Selector.WithText(text));

    /// <summary>Double-clicks where a press reaches the node <paramref name="selector"/> names.</summary>
    public UiFrames DoubleClickOn(Selector selector)
    {
        var at = Frames.Target(selector) switch
        {
            TargetResult.Hittable hittable => hittable.At,
            var other => throw new InvalidOperationException($"Cannot double-click {selector}: {other}"),
        };
        return Frames.DoubleClick(at.X, at.Y);
    }
}
