using System.Threading.Channels;
using ZGF.AppUtils;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ILSight.MainArea;
using ILSight.SettingArea;

namespace ILSight.ILSightAppArea;

/// <summary>A class because ZGF's ImmediateGuiApp, its base class, calls it for each step of the app's life. The
/// app: keeps the app state and the screen in step around each frame, through <see cref="AppFrames"/>, and
/// carries the commands each queues for the other. Neither state sees the other; only the app and the views
/// see both.</summary>
[Edge]
public sealed class ILSightApp : ImmediateGuiApp<UiState>
{
    private const string MonoFontFile = "JetBrainsMono-Regular.ttf";
    private const string ItalicFontFile = "Inter-Italic.ttf";
    private const string AppIconFile = "app_icon.rgba";

    private readonly AppState _appState;
    private readonly ListStart _start;
    private readonly Channel<RunArguments> _inbox;
    private readonly CodeFontFaces _codeFonts = new() { Bundled = ReadFont(MonoFonts.Code, MonoFontFile) };

    /// <param name="appState">The app state, its start recorded on it.</param>
    /// <param name="uiState">The screen over it.</param>
    /// <param name="start">The start the root recorded on <paramref name="appState"/>, for this app to start.</param>
    /// <param name="inbox">Where the arguments of this run, put there first, and of the runs handed to it arrive.</param>
    internal ILSightApp(
        AppState appState,
        UiState uiState,
        ListStart start,
        Channel<RunArguments> inbox) : base(uiState)
    {
        _appState = appState;
        _start = start;
        _inbox = inbox;
    }

    protected override void OnStartup(ImmediateGuiAppHost<UiState> host)
    {
        ShowAppIcon(host);
        RegisterFonts(host);
        RegisterChosenCodeFont(host);
        RegisterChosenIconFonts(host);
        StartRedrawingOnAssemblyChanges(host);
        AppStart.Start(_appState, _start, _inbox);
    }

    protected override void OnUpdate(Ui<UiState> ui)
    {
        RunFrame(ui, MainView.Render);
    }

    /// <summary>One frame: the frame edge before <paramref name="draw"/>, the draw, and the frame edge after it,
    /// which runs even when the draw fails, so every command the draw queued before failing starts: the state the
    /// draw changed already says it has.</summary>
    internal void RunFrame(Ui<UiState> ui, Action<Ui<UiState>, AppState> draw)
    {
        AppFrames.StartFrame(ui.State, _appState);
        AppFrames.FollowNuGetPicker(ui, _appState);
        Draw(ui, draw);
        AppFrames.FinishFrame(ui, _appState, _codeFonts);
        QuitIfAsked(ui);
    }

    // The app asks to quit to restart into an update; as with a request to close the window, a refusal leaves it open,
    // and the next frame shows why.
    private void QuitIfAsked(Ui<UiState> ui)
    {
        if (!ui.State.QuitAsked)
        {
            return;
        }

        ui.State.QuitAsked = false;
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
    /// while a run sending one can still be told to open its own: no frame runs after this to follow it.</summary>
    internal bool TryClose(WindowBounds window)
    {
        var write = SaveOnExit(window);
        if (!_appState.Closing.TryClose(write))
        {
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
        host.RegisterFont(_codeFonts.Bundled);
        host.RegisterFont(ReadFont(TextFonts.Italic, ItalicFontFile));
    }

    // Before the first frame, so code never shows in the bundled font first.
    private void RegisterChosenCodeFont(ImmediateGuiAppHost<UiState> host)
    {
        var face = AppFrames.TakeCodeFontFace(UiState, _codeFonts);
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
        if (!UiState.Shown.TryTakeIcons(UiState.Icons, UiState.IconFonts))
        {
            return;
        }

        foreach (var font in UiState.IconFonts)
        {
            host.RegisterFont(font);
        }
    }

    private void StopRedrawingOnAssemblyChanges(ImmediateGuiAppHost<UiState> host)
    {
        _appState.Assemblies.Changed -= host.Invalidate;
    }

    private static FontFace ReadFont(string family, string file)
    {
        var data = EmbeddedAssets.LoadBytes(typeof(ILSightApp).Assembly, file);
        return new FontFace(family, data);
    }

    private void StartRedrawingOnAssemblyChanges(ImmediateGuiAppHost<UiState> host)
    {
        _appState.Assemblies.Changed += host.Invalidate;
    }

    private SettingsWriteResult SaveOnExit(WindowBounds window)
    {
        _appState.KeptScreen.KeepWindow(window);
        _appState.KeptScreen.KeepView(ScreenSettings.TakeViewSettings(UiState));
        return _appState.SettingsSaving.Save(_appState);
    }
}
