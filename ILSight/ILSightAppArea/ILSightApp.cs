using System.Threading.Channels;
using ZGF.AppUtils;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ILSight.CommandArea;
using ILSight.MainArea;
using static ILSight.Analysis;
using static ILSight.ILSightAppArea.AppFrames;
using static ILSight.ILSightAppArea.Appearance;
using ILSight.AssemblyListArea;
using static ILSight.ILSightAppArea.RunArgumentsActions;
using static ILSight.AssemblyArea.Assemblies;
using static ILSight.IconTheming;
using static ILSight.SettingArea.Settings;
using static ILSight.ILSightAppArea.ScreenSettings;
using static ILSight.Theming;

namespace ILSight.ILSightAppArea;

/// <summary>A class because ZGF's ImmediateGuiApp, its base class, calls it for each step of the app's life. The
/// app: keeps the app state and the screen in step around each frame, through <see cref="AppFrames"/>, and
/// carries the commands each queues for the other. Neither state sees the other; only the app and the views
/// see both.</summary>
[Edge]
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
        Commands.StartShowing(_appState, nameof(OpenStartupAssembliesAsync), ct => OpenStartupAssembliesAsync(_appState, _startupFiles, ct));
        StartRedrawingOnAssemblyChanges(host);
        Commands.StartRestoringAnalyses(_appState, Analysis.BeginRestoring(_appState.Analyzer, _appState.Assemblies));
        WatchUserThemesOnStartup(_appState);
        CheckForUpdatesOnStartup(_appState);
        _appState.Workflows.StartUntilShutdown(ApplyPendingArgumentsAsync);
    }

    protected override void OnUpdate(Ui<UiState> ui)
    {
        StartFrame(ui.State, _appState);
        FollowNuGetPicker(ui, _appState);
        FollowProcessPicker(ui.State, _appState);
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
        _appState.Workflows.CancelAll();
        StopRedrawingOnAssemblyChanges(host);
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
        if (!ShownChanges.TryTakeIcons(UiState, UiState.Icons, UiState.IconFonts))
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
    /// each one arriving wakes the app. Each is a workflow of its own, so arguments that fail are reported by
    /// the workflow runner rather than ending this loop, which would leave the pipe answering later runs with no
    /// one to follow them.</summary>
    internal async Task ApplyPendingArgumentsAsync(CancellationToken ct)
    {
        await foreach (var arguments in _inbox.Reader.ReadAllAsync(ct))
        {
            ReportIgnored(arguments);
            ApplyArguments(arguments);
            UiState.FrontAsked = true;
        }
    }

    /// <summary>Does what the arguments a later run handed over ask of the open window: decompiles to their language,
    /// switches to their list (added when there is none), clears the list, then opens their files and shows what they
    /// name. Each change is made here, in that order, and the work each hands back starts at once as a workflow of its
    /// own: the list's assemblies opening and the finding of its analyses, the lookup of the tabs clearing the list left
    /// waiting, and opening the files. None waits for another: the tabs left waiting say what is gone while the files
    /// open.</summary>
    private void ApplyArguments(RunArguments arguments)
    {
        var appState = _appState;
        if (arguments.Language is not null)
        {
            appState.Language = arguments.Language.Value;
        }

        if (arguments.AssemblyList is not null)
        {
            Commands.StartOpeningList(appState, AssemblyLists.SwitchToListNamed(appState.Lists, appState.ListSession, appState.View, arguments.AssemblyList));
        }

        if (arguments.ClearList)
        {
            Commands.StartLoads(appState, CloseAllAssemblies(appState.ListSession));
        }

        var files = arguments.Files;
        Commands.StartShowing(appState, nameof(OpenRequestedFilesAsync), ct => OpenRequestedFilesAsync(appState, files, ct));
    }

    /// <summary>Tells the user of the arguments of <paramref name="arguments"/> that were ignored, if any.</summary>
    private void ReportIgnored(RunArguments arguments)
    {
        var ignored = RunArgumentsActions.FindIgnored(arguments);
        if (ignored is null)
        {
            return;
        }

        Errors.Report(_appState, ignored);
    }

    internal void SaveOnExit(WindowBounds window)
    {
        KeepWindow(_appState, window);
        KeepViewSettings(_appState, TakeViewSettings(UiState));
        _appState.SettingsSaving.Save(_appState);
    }
}
