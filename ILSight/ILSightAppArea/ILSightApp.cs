using System.Threading.Channels;
using ZGF.AppUtils;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ILSight.MainArea;
using ILSight.SettingArea;

namespace ILSight.ILSightAppArea;

/// <summary>A class because ZGF's ImmediateGuiApp, its base class, calls it for each step of the app's life. The
/// window: runs the app's loop (<see cref="AppLoop"/>), which keeps the app state and the screen in step around each
/// frame and carries the commands each queues for the other, and does what only the window can: the scale, the fonts,
/// the shell's requests, coming to the front and quitting. Neither state sees the other; only the loop, the window and
/// the views see both.</summary>
[Edge]
public sealed class ILSightApp : ImmediateGuiApp<UiState>
{
    private const string MonoFontFile = "JetBrainsMono-Regular.ttf";
    private const string ItalicFontFile = "Inter-Italic.ttf";
    private const string AppIconFile = "app_icon.rgba";

    private readonly AppState _appState;
    private readonly AppLoop _loop;
    private readonly WindowFrameHost _frame = new();
    private readonly Channel<RunArguments> _inbox;
    private readonly IWindowFront _windowFront;
    private readonly CodeFontReader _codeFontReader;

    /// <param name="appState">The app state, its start recorded on it.</param>
    /// <param name="uiState">The screen over it.</param>
    /// <param name="loop">The app's loop the root built over <paramref name="appState"/>, for this window to run.</param>
    /// <param name="inbox">Where the arguments of this run, put there first, and of the runs handed to it arrive.</param>
    /// <param name="windowFront">The port the window is brought to the front through.</param>
    internal ILSightApp(
        AppState appState,
        UiState uiState,
        AppLoop loop,
        Channel<RunArguments> inbox,
        IWindowFront windowFront) : base(uiState)
    {
        _appState = appState;
        _loop = loop;
        _inbox = inbox;
        _windowFront = windowFront;
        var bundledCodeFace = ReadFont(MonoFonts.Code, MonoFontFile);
        _codeFontReader = new CodeFontReader(bundledCodeFace);
    }

    protected override void OnStartup(ImmediateGuiAppHost<UiState> host)
    {
        ShowAppIcon(host);
        RegisterFonts(host);
        RegisterChosenCodeFont(host);
        RegisterChosenIconFonts(host);
        StartRedrawingOnAssemblyChanges(host);
        _loop.Start();
    }

    protected override void OnUpdate(Ui<UiState> ui)
    {
        RunFrame(ui, MainView.Render);
    }

    /// <summary>One frame: the loop's calls before <paramref name="draw"/>, the draw, and the loop's calls after it,
    /// ending in the loop's save of the changed settings, which run even when the draw fails, so every command the draw queued before
    /// failing starts: the state the draw changed already says it has. Then the window's own: it comes back to try the
    /// save again when a write waits or failed, draws at the chosen scale, code in the chosen font
    /// and icons in their fonts from the next frame, opens the web pages, files and folders asked for, comes to the
    /// front when a later run asked for it, and quits when asked to.</summary>
    internal void RunFrame(Ui<UiState> ui, Action<Ui<UiState>, AppState> draw)
    {
        _frame.Enter(ui);
        _loop.BeforeDraw(ui.State, _frame);
        Draw(ui, draw);
        var settingsSave = _loop.AfterDraw(ui.State, _frame);
        RetrySaveWhenAsked(ui, settingsSave);
        DrawAtChosenScale(ui, ui.State.Display);
        DrawCodeInChosenFont(ui, ui.State.Display, ui.State.Shown);
        SwapIconFontsWhenThemeChanges(ui, ui.State.Appearance, ui.State.Shown);
        OpenAskedWebPages(ui, ui.State.Shell);
        OpenAskedFiles(ui, ui.State.Shell);
        OpenAskedFolders(ui, ui.State.Shell);
        BringToFrontIfAsked(ui);
        QuitIfAsked(ui, ui.State.Shell);
    }

    // The app asks to quit to restart into an update; as with a request to close the window, a refusal leaves it open,
    // and the next frame shows why.
    private void QuitIfAsked(Ui ui, ShellRequests shell)
    {
        if (!shell.TryTakeQuit())
        {
            return;
        }

        if (!TryClose(ui.WindowBounds))
        {
            ui.RequestUpdate();
            return;
        }

        ui.Quit();
    }

    private void Draw(Ui<UiState> ui, Action<Ui<UiState>, AppState> draw)
    {
        try
        {
            draw(ui, _appState);
        }
        catch (Exception ex)
        {
            Errors.Report(_appState, new AppProblem.Unexpected(ex));
            ui.RequestUpdate();
        }
    }

    private static void RetrySaveWhenAsked(Ui ui, Saving autosave)
    {
        var retryIn = autosave.Match<TimeSpan?>(
            static _ => null,
            static _ => null,
            static waiting => waiting.RetryIn,
            static failed => failed.RetryIn);
        if (retryIn is null)
        {
            return;
        }

        ui.RequestUpdateIn(retryIn.Value.TotalSeconds);
    }

    private void BringToFrontIfAsked(Ui ui)
    {
        if (!_appState.FrontAsked)
        {
            return;
        }

        _appState.FrontAsked = false;
        _windowFront.BringToFront(ui);
    }

    private static void OpenAskedWebPages(Ui ui, ShellRequests shell)
    {
        while (shell.TryTakeWebPage(out var url))
        {
            ui.OpenUrl(url);
        }
    }

    private static void OpenAskedFolders(Ui ui, ShellRequests shell)
    {
        while (shell.TryTakeFolder(out var path))
        {
            ui.OpenFolder(path);
        }
    }

    private static void OpenAskedFiles(Ui ui, ShellRequests shell)
    {
        while (shell.TryTakeFile(out var path))
        {
            ui.OpenFile(path);
        }
    }

    // When the icon theme changed: unregisters the fonts it dropped, registers the ones it uses.
    private static void SwapIconFontsWhenThemeChanges(Ui ui, Appearance appearance, ShownChanges shown)
    {
        var before = shown.IconFonts;
        if (!shown.TryTakeIcons(appearance.Icons, appearance.IconFonts))
        {
            return;
        }

        foreach (var family in ShownChanges.ListDroppedFamilies(before, appearance.IconFonts))
        {
            ui.UnregisterFont(family);
        }

        foreach (var font in appearance.IconFonts)
        {
            ui.RegisterFont(font);
        }

        ui.ReleaseUndrawnIconImages();
    }

    private static void DrawAtChosenScale(Ui ui, ScreenDisplay display)
    {
        ui.Scale = display.Options.UiScale;
    }

    private void DrawCodeInChosenFont(Ui ui, ScreenDisplay display, ShownChanges shown)
    {
        var face = _codeFontReader.TakeFace(display, shown);
        if (face == null)
        {
            return;
        }

        ui.RegisterFont(face);
    }

    /// <summary>Asks <see cref="TryClose"/> whether the window may close; when it may not, the next frame shows
    /// why.</summary>
    protected override bool OnCloseRequested(ImmediateGuiAppHost<UiState> host)
    {
        var closes = TryClose(host.WindowBounds);
        if (!closes)
        {
            host.Invalidate();
        }

        return closes;
    }

    /// <summary>Saves the settings, with the window at <paramref name="window"/>, as the user asks to close it, while it
    /// can still show a failure; true when it may close, as <see cref="WindowClosing"/> answers from the save (a failed
    /// one keeps it open once, the error panel showing why in the next frame). Arguments are refused once it closes,
    /// while a run sending one can still be told to open its own: no frame runs after this to take it.</summary>
    internal bool TryClose(WindowBounds window)
    {
        var write = SaveOnExit(window);
        if (!_appState.Closing.TryClose(write))
        {
            // What the user just asked for failed: said where they acted, the window they asked to close, by opening
            // the errors on why. Errors nobody asked about are only counted on the status bar.
            UiState.ErrorDialog.Open(_appState.Errors.Entries);
            return false;
        }

        _inbox.Writer.TryComplete();
        return true;
    }

    protected override void OnShutdown(ImmediateGuiAppHost<UiState> host)
    {
        _inbox.Writer.TryComplete();
        _appState.Workflows.CancelAll();
        StopRedrawingOnAssemblyChanges(host);
    }

    private static void ShowAppIcon(ImmediateGuiAppHost<UiState> host)
    {
        var packed = EmbeddedAssets.LoadBytes(typeof(ILSightApp).Assembly, AppIconFile);
        host.SetIcon(WindowIcon.FromPacked(packed));
    }

    private void RegisterFonts(ImmediateGuiAppHost<UiState> host)
    {
        host.RegisterFont(_codeFontReader.Bundled);
        host.RegisterFont(ReadFont(TextFonts.Italic, ItalicFontFile));
    }

    // Before the first frame, so code never shows in the bundled font first.
    private void RegisterChosenCodeFont(ImmediateGuiAppHost<UiState> host)
    {
        var face = _codeFontReader.TakeFace(UiState.Display, UiState.Shown);
        if (face == null)
        {
            return;
        }

        host.RegisterFont(face);
    }

    // Before the first frame, so icons never show in another font first and the first frame finds them registered.
    private void RegisterChosenIconFonts(ImmediateGuiAppHost<UiState> host)
    {
        UiState.Appearance.ApplyIconTheme(IconTheming.ResolveIconTheme(_appState));
        if (!UiState.Shown.TryTakeIcons(UiState.Appearance.Icons, UiState.Appearance.IconFonts))
        {
            return;
        }

        foreach (var font in UiState.Appearance.IconFonts)
        {
            host.RegisterFont(font);
        }
    }

    private void StopRedrawingOnAssemblyChanges(ImmediateGuiAppHost<UiState> host)
    {
        _appState.ListSession.Assemblies.Changed -= host.Invalidate;
    }

    private static FontFace ReadFont(string family, string file)
    {
        var data = EmbeddedAssets.LoadBytes(typeof(ILSightApp).Assembly, file);
        return new FontFace(family, data);
    }

    private void StartRedrawingOnAssemblyChanges(ImmediateGuiAppHost<UiState> host)
    {
        _appState.ListSession.Assemblies.Changed += host.Invalidate;
    }

    private SettingsWriteResult SaveOnExit(WindowBounds window)
    {
        _appState.KeptScreen.KeepWindow(window);
        _appState.KeptScreen.KeepView(ScreenSettings.TakeViewSettings(UiState));
        return _appState.SettingsSaving.Save(_appState);
    }
}
