using System.Threading.Channels;
using ZGF.Gui.Immediate;
using ZGF.Gui.Testing;
using ILSight.Core;
using ILSight.Commanding;
using ILSight.AppSettingsFormat;

namespace ILSight.App.Tests;

/// <summary>Renders the whole app window frame by frame over a <see cref="AppStateDriver"/>, which
/// it pumps before each frame.</summary>
internal sealed class AppDriver
{
    public AppDriver(float width = 1000f, float height = 700f, AppSettings? settings = null, RunArguments? queuedArguments = null, ITextMeasurer? measurer = null, Channel<RunArguments>? inbox = null,
        HttpMessageHandler? nuGetRequests = null)
    {
        Driver = new AppStateDriver(settings, nuGetRequests);
        inbox ??= Channel.CreateUnbounded<RunArguments>();
        if (queuedArguments != null)
        {
            inbox.Writer.TryWrite(queuedArguments);
        }

        App = new ILSightApp(AppState, Ui, CommandLines.Parse([]).Arguments.Files, inbox);
        Frames = UiFrames.Running(App, measurer, width: width, height: height, background: AppState.Work);
        Frames.FrameTime = 1f / 60f;
        // What the test starts outside a frame comes back through the test's queue, which wakes these frames, so
        // waiting for the UI to be ready runs the frame that pumps it. UiFrames hands out its Ui only to a frame,
        // so one empty frame, which takes no time, fetches it.
        var ui = Frames.Run(0f, static ui => ui);
        Driver.WakeOnPost(ui.RequestFrame);
        AppState.Workflows.StartUntilShutdown(App.ApplyPendingArgumentsAsync);
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

    public BrowseNode Node(params ReadOnlySpan<string> labels)
    {
        return Driver.Node(labels);
    }

    public BrowseNode Fixture(string type)
    {
        return Driver.Fixture(type);
    }

    public void Run(int frames = 1)
    {
        Frames.Frame(frames);
    }

    /// <summary>Goes to the history entry at <paramref name="index"/>, as <see cref="AppStateDriver.GoToEntry"/> does.</summary>
    public void GoToEntry(int index)
    {
        Driver.GoToEntry(index);
    }

    /// <summary>Goes back in the history, as <see cref="AppStateDriver.GoBack"/> does.</summary>
    public void GoBack()
    {
        Driver.GoBack();
    }

    /// <summary>Goes forward in the history, as <see cref="AppStateDriver.GoForward"/> does.</summary>
    public void GoForward()
    {
        Driver.GoForward();
    }

    /// <summary>Starts <paramref name="workflow"/> over the app state now, as <see cref="AppStateDriver.Start"/> does.</summary>
    public void Start(Func<AppState, CancellationToken, Task> workflow)
    {
        Driver.Start(workflow);
    }

    /// <summary>Starts <paramref name="show"/> over the app state now, then the load it returns, as
    /// <see cref="AppStateDriver.Start(Func{AppState, CancellationToken, Task{DecompileTicket?}})"/> does.</summary>
    public void Start(Func<AppState, CancellationToken, Task<DecompileTicket?>> show)
    {
        Driver.Start(show);
    }

    /// <summary>Starts <paramref name="show"/> over the app state now, then each load it returns.</summary>
    public void Start(Func<AppState, CancellationToken, Task<List<DecompileTicket>>> show)
    {
        Driver.Start(show);
    }

    /// <summary>Starts <paramref name="show"/> over the app state now, then each load it returns.</summary>
    public void Start(Func<AppState, CancellationToken, Task<TabLoads>> show)
    {
        Driver.Start(show);
    }


    /// <summary>Waits until the UI is ready, with the background work done and a frame run after it, then runs
    /// <paramref name="frames"/> frames.</summary>
    public void Settle(int frames = 0)
    {
        Frames.WaitUntilReady(Patience);
        Run(frames);
    }

    /// <summary>Runs out what waits on the clock and the work it starts: virtual time until only ambient updates are
    /// pending, as a search's debounce, then the background work that started, then the clock again for what
    /// that set moving, as cards gliding to a new layout.</summary>
    public void RunOut()
    {
        Frames.Settle(SettleSeconds);
        Settle();
        Frames.Settle(SettleSeconds);
    }

    /// <summary>Runs frames without advancing time, each pumping the app state first, until none is asked for.</summary>
    private void RunFramesInPlace()
    {
        Frames.WaitUntil(static () => true, Patience);
    }


    /// <summary>Shows <paramref name="node"/> as <see cref="AppStateDriver.Show"/>, then runs <paramref name="frames"/> frames.</summary>
    public void Show(BrowseNode node, int frames = 0, bool inNewTab = false)
    {
        Driver.Show(node, inNewTab);
        Run(frames);
    }

    /// <summary>Starts running <paramref name="run"/>, as <see cref="AppStateDriver.StartComparing"/> does.</summary>
    public void StartComparing(ComparisonRun? run)
    {
        Driver.StartComparing(run);
    }

    public void Analyze(BrowseNode node)
    {
        Driver.Analyze(node);
    }

    public void OpenExportSetup(LoadedModule? picked)
    {
        Driver.OpenExportSetup(picked);
    }

    /// <summary>Where <paramref name="icon"/> was drawn on the last frame, in window coordinates.</summary>
    public UiRect TextRect(IconGlyph icon)
    {
        return TextRect(BuiltInGlyphs.Text(icon));
    }

    /// <summary>Where <paramref name="icon"/> was last drawn, as <see cref="LastTextRect(string)"/>.</summary>
    public UiRect LastTextRect(IconGlyph icon)
    {
        return LastTextRect(BuiltInGlyphs.Text(icon));
    }

    /// <summary>Where the text was drawn on the last frame, in window coordinates.</summary>
    public UiRect TextRect(string text)
    {
        return Drawn(text, Frames.Canvas.Texts.FirstOrDefault(t => t.Inputs.Text == text));
    }

    /// <summary>Where <paramref name="text"/> was last drawn: the one furthest into the window when
    /// the same text, such as an icon, is also drawn before it.</summary>
    public UiRect LastTextRect(string text)
    {
        return Drawn(text, Frames.Canvas.Texts.LastOrDefault(t => t.Inputs.Text == text));
    }

    private UiRect Drawn(string text, RecordedText? drawn)
    {
        if (drawn == null)
        {
            throw new InvalidOperationException($"'{text}' was not drawn: {string.Join(", ", Frames.Canvas.Texts.Select(t => t.Inputs.Text))}");
        }

        return Frames.ToUi(drawn.Inputs.Position);
    }

    /// <summary>Where the tab titled <paramref name="title"/> was drawn on the last frame; the first of
    /// those drawn when several tabs are.</summary>
    public UiRect TabTitleRect(string title)
    {
        return ListTabTitleRects(title)[0];
    }

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
        {
            throw new InvalidOperationException($"'{title}' was not drawn.");
        }

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
        if (drawn is null)
        {
            throw new InvalidOperationException($"'{text}' was not drawn in the status bar.");
        }

        return rect;
    }

    private float StatusBarTop => Frames.Height - Theme.StatusBarHeight - 0.5f;

    /// <summary>Right-clicks the tree row labelled <paramref name="row"/> and picks <paramref name="item"/>
    /// from its menu.</summary>
    public void PickFromTreeMenu(string row, string item)
    {
        Frames.ClickOn(Selector.OfRole(SemanticRole.TreeItem, row), PointerButton.Right).Frame();
        Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, item)).Frame();
    }

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
