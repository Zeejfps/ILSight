using System.Threading.Channels;
using ZGF.AppUtils;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using static ILSight.Analysis;
using static ILSight.AppFrames;
using static ILSight.Appearance;
using static ILSight.RunArgumentsActions;
using static ILSight.IconTheming;
using static ILSight.Settings;
using static ILSight.ScreenSettings;
using static ILSight.Theming;
using static ILSight.Updates;

namespace ILSight;

/// <summary>A class because ZGF's ImmediateGuiApp, its base class, calls it for each step of the app's life. The
/// app: keeps the app state and the screen in step around each frame, through <see cref="AppFrames"/>, and
/// carries the commands each queues for the other. Neither state sees the other; only the app and the views
/// see both.</summary>
public sealed partial class ILSightApp : ImmediateGuiApp<UiState>
{
    private const string MonoFontFile = "JetBrainsMono-Regular.ttf";
    private const string ItalicFontFile = "Inter-Italic.ttf";
    private const string AppIconFile = "app_icon.rgba";

    private readonly AppState _appState;
    private readonly FilesToOpen _startupFiles;
    private readonly Channel<RunArguments> _inbox;
    private readonly CodeFontFaces _codeFonts = new() { Bundled = ReadFont(MonoFonts.Code, MonoFontFile) };

    internal ILSightApp(
        AppState appState,
        UiState uiState,
        FilesToOpen startupFiles,
        Channel<RunArguments> inbox) : base(uiState)
    {
        _appState = appState;
        _startupFiles = startupFiles;
        _inbox = inbox;
    }

    protected override void OnStartup(ImmediateGuiAppHost<UiState> host)
    {
        ShowAppIcon(host);
        RegisterFonts(host);
        RegisterChosenCodeFont(host);
        RegisterChosenIconFonts(host);
        OpenStartupAssemblies(_appState, _startupFiles);
        StartRedrawingOnAssemblyChanges(host);
        RestoreAnalyses(_appState);
        WatchUserThemes(_appState);
        WatchUserIconThemes(_appState);
        CheckOnStartup(_appState);
        Background.StartForever(ApplyPendingArgumentsAsync(), problem => Errors.Report(_appState, problem));
    }

    protected override void OnUpdate(Ui<UiState> ui)
    {
        StartFrame(ui.State, _appState);
        MainView.Render(ui, _appState);
        FinishFrame(ui, _appState, _codeFonts);
    }

    /// <summary>Refuses arguments as the window closes, while a run sending one can still be told to
    /// open its own: no frame runs after this to follow it.</summary>
    protected override bool OnCloseRequested(ImmediateGuiAppHost<UiState> host)
    {
        _inbox.Writer.TryComplete();
        return true;
    }

    protected override void OnShutdown(ImmediateGuiAppHost<UiState> host)
    {
        _inbox.Writer.TryComplete();
        StopRedrawingOnAssemblyChanges(host);
        StopWatchingUserThemes(_appState);
        StopWatchingUserIconThemes(_appState);
        SaveOnExit(host.WindowBounds);
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

    /// <summary>Registers the chosen code font before the first frame, so code never shows in the
    /// bundled one first.</summary>
    private void RegisterChosenCodeFont(ImmediateGuiAppHost<UiState> host)
    {
        var face = TakeCodeFontFace(UiState, _codeFonts);
        if (face == null)
        {
            return;
        }

        host.RegisterFont(face);
    }

    /// <summary>Hands the screen the chosen icon theme and registers its fonts before the first frame, so
    /// icons never show in another font first and the first frame finds them registered.</summary>
    private void RegisterChosenIconFonts(ImmediateGuiAppHost<UiState> host)
    {
        ApplyIconTheme(UiState, ResolveIconTheme(_appState));
        if (!ShownChanges.TakeIcons(UiState, UiState.Icons, UiState.IconFonts))
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

    /// <summary>Leaves the arguments of later runs to windows of their own once this one closes.</summary>
    /// <summary>Does what the arguments of later runs ask as they arrive, until this window closes,
    /// bringing the window to the front for each. Started on the UI thread, so it carries on there, and
    /// each one arriving wakes the app.</summary>
    internal async Task ApplyPendingArgumentsAsync()
    {
        await foreach (var arguments in _inbox.Reader.ReadAllAsync())
        {
            try
            {
                ApplyArguments(_appState, arguments);
            }
            // Catches everything so that arguments that fail are reported rather than ending
            // this loop, which would leave the pipe answering later runs with no one to follow them.
            catch (Exception e)
            {
                Errors.Report(_appState, new AppProblem.Unexpected(e));
            }
            UiState.FrontAsked = true;
        }
    }

    internal void SaveOnExit(WindowBounds window)
    {
        KeepWindow(_appState, window);
        KeepViewSettings(_appState, TakeViewSettings(UiState));
        SaveSettings(_appState);
    }
}
