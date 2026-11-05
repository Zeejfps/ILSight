using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using ILSight.Core;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using static ILSight.Bookmarks;
using static ILSight.Comparisons;
using static ILSight.Diagrams;
using static ILSight.Exports;
using static ILSight.Navigation;
using static ILSight.Preferences;
using static ILSight.ScreenLayout;

namespace ILSight;

/// <summary>Plays one scene of the trailer in the real app and saves each frame the renderer draws,
/// with the time it was drawn at. Only on the trailer-capture branch; asked for with
/// ILSIGHT_TRAILER_SCENE and ILSIGHT_TRAILER_OUT, and starts once ILSIGHT_TRAILER_OUT holds a file
/// named go, which the capture script writes after sizing the window.</summary>
internal sealed class TrailerDirector
{
    private const float AnalyzerRatio = 0.52f;
    // On screen in the export scene, so it names nobody; deleted after the capture.
    private const string ExportFolder = @"G:\Exports\System.Private.CoreLib";
    private const uint CharacterMessage = 0x0102;
    private const uint KeyDownMessage = 0x0100;
    private const uint KeyUpMessage = 0x0101;
    private const int EscapeKey = 0x1B;
    private const string GoFile = "go";
    private const string TimesFile = "times.txt";

    /// <summary>Asks the diagram on screen to fit, as its Fit to view button does.</summary>
    public static bool FitDiagram;

    /// <summary>Asks the menu bar to open File on its next frame.</summary>
    public static bool OpensFileMenu;

    /// <summary>What the tree is to open its context menu on, on its next frame.</summary>
    public static BrowseNode? TreeMenuNode;

    private readonly AppState _app;
    private readonly ImmediateGuiAppHost<UiState> _host;
    private readonly string _output;
    private readonly IEnumerator<Func<bool>> _script;
    private readonly List<(string File, double Seconds)> _frames = new();
    private readonly Stopwatch _clock = new();
    private Func<bool>? _waiting;
    private bool _recording;
    private bool _saving;
    private UiState? _screen;

    public TrailerDirector(AppState app, ImmediateGuiAppHost<UiState> host, string scene, string output)
    {
        _app = app;
        _host = host;
        _output = output;
        _script = Play(scene).GetEnumerator();
        DrawEverythingInTheMainWindow(host);
    }

    /// <summary>The director of the scene the environment asks for, else null.</summary>
    public static TrailerDirector? FromEnvironment(AppState app, ImmediateGuiAppHost<UiState> host)
    {
        var scene = Environment.GetEnvironmentVariable("ILSIGHT_TRAILER_SCENE");
        var output = Environment.GetEnvironmentVariable("ILSIGHT_TRAILER_OUT");
        if (scene == null || output == null)
            return null;
        return new TrailerDirector(app, host, scene, output);
    }

    public void Step(Ui<UiState> ui)
    {
        _screen = ui.State;
        ui.RequestUpdateIn(0f);
        if (!File.Exists(Path.Combine(_output, GoFile)))
            return;
        SaveFrame();
        if (_waiting != null && !_waiting())
            return;
        _waiting = null;
        if (_script.MoveNext())
        {
            _waiting = _script.Current;
            return;
        }
        _recording = false;
        _waiting = () => !_saving;
        if (!_saving)
            Finish();
    }

    private void SaveFrame()
    {
        if (!_recording || _saving)
            return;
        _saving = true;
        var seconds = _clock.Elapsed.TotalSeconds;
        var file = $"{_frames.Count:D5}.png";
        _frames.Add((file, seconds));
        _host.RequestScreenshot(Path.Combine(_output, file), () => _saving = false);
    }

    private void Finish()
    {
        var lines = _frames.Select(f => $"{f.File} {f.Seconds.ToString("F4", CultureInfo.InvariantCulture)}");
        File.WriteAllLines(Path.Combine(_output, TimesFile), lines);
        Environment.Exit(0);
    }

    private IEnumerable<Func<bool>> Play(string scene) => scene switch
    {
        "languages" => Languages(),
        "themes" => Themes(),
        "analyzer" => Analyzer(),
        "diagram" => Diagram(),
        "compare" => Compare(),
        "bookmarks" => BookmarksScene(),
        "goto" => GoTo(),
        "nuget" => NuGet(),
        "export" => Export(),
        _ => throw new InvalidOperationException($"No scene called {scene}."),
    };

    private Func<bool> Hold(double seconds)
    {
        var until = Stopwatch.GetTimestamp() + (long)(seconds * Stopwatch.Frequency);
        return () => Stopwatch.GetTimestamp() >= until;
    }

    private bool IsIdle()
    {
        var tab = _app.Tabs.Active;
        return tab != null && !tab.IsBusy;
    }

    private bool HasAssembly() => _app.Assemblies.Assemblies.OfType<LoadedModule>().Any();

    private void Record()
    {
        _clock.Restart();
        _recording = true;
    }

    private BrowseNode Find(string space, string type, string member)
    {
        var session = _app.Session;
        var module = _app.Assemblies.Assemblies.OfType<LoadedModule>().First();
        var inSpace = session.ChildrenOf(session.RootOf(module)).First(n => n.Text == space);
        var inType = session.ChildrenOf(inSpace).First(n => n.Text == type);
        return session.ChildrenOf(inType).First(n => n.Text.StartsWith(member, StringComparison.Ordinal));
    }

    private IEnumerable<Func<bool>> Show(BrowseNode node)
    {
        ShowNode(_app, node);
        yield return Hold(0.3);
        yield return IsIdle;
    }

    private IEnumerable<Func<bool>> Languages()
    {
        yield return HasAssembly;
        yield return Hold(1);
        foreach (var wait in Show(Find("Newtonsoft.Json", "JsonConvert", "ToString(bool")))
            yield return wait;
        yield return Hold(0.5);
        Record();
        yield return Hold(1);
        foreach (var language in new[] { DecompilationLanguage.IL, DecompilationLanguage.ILWithCSharp })
        {
            SetLanguage(_app, language);
            yield return Hold(0.3);
            yield return IsIdle;
            yield return Hold(1);
        }
    }

    private IEnumerable<Func<bool>> Themes()
    {
        yield return HasAssembly;
        yield return Hold(1);
        foreach (var wait in Show(Find("Newtonsoft.Json", "JsonConvert", "ToString(bool")))
            yield return wait;
        yield return Hold(0.5);
        Record();
        yield return Hold(0.5);
        foreach (var theme in new[] { "catppuccin-mocha", "dracula", "github-light" })
        {
            SetTheme(_app, ThemeId<ThemeFile>.Of(theme));
            yield return Frames(2);
        }
    }

    private IEnumerable<Func<bool>> Analyzer()
    {
        yield return HasAssembly;
        yield return Hold(1);
        var member = Find("Newtonsoft.Json", "JsonSerializer", "CreateDefault()");
        foreach (var wait in Show(member))
            yield return wait;
        yield return Hold(0.5);
        Record();
        yield return Frames(1);
        foreach (var wait in ShowMenu(() => TreeMenuNode = member))
            yield return wait;
        AnalyzerPanelView.Analyze(_screen!, _app, member);
        GiveTheAnalyzerRoom(_screen!.MainLayout.Dock.Root);
        yield return () => _app.Analyzer.Roots.OfType<AnalyzerSymbolNode>().Any();
        var root = _app.Analyzer.Roots.OfType<AnalyzerSymbolNode>().First();
        var rows = _screen!.Analyzer;
        rows.View.SetExpanded(root, true);
        yield return Hold(1);
        var usedBy = rows.GetChildren(root).OfType<AnalyzerKindNode>().First(k => k.Kind.Header == "Used By");
        rows.View.SetExpanded(usedBy, true);
        rows.GetChildren(usedBy);
        yield return () => usedBy.IsDone;
        yield return Hold(1.5);
    }

    private IEnumerable<Func<bool>> Diagram()
    {
        yield return HasAssembly;
        yield return Hold(1);
        var session = _app.Session;
        var module = _app.Assemblies.Assemblies.OfType<LoadedModule>().First();
        var space = session.ChildrenOf(session.RootOf(module)).First(n => n.Text == "System.Threading.Channels");
        var type = session.ChildrenOf(space).First(n => n.Text == "ChannelOptions");
        ShowInNewDiagram(_app, NodeDefinitions.ReferenceOf(type.Subject)!, "System.Threading.Channels");
        yield return Hold(2);
        FitDiagram = true;
        yield return Hold(1);
        FitDiagram = false;
        yield return Hold(0.5);
        Record();
        yield return Hold(1.5);
    }

    private IEnumerable<Func<bool>> Compare()
    {
        yield return () => _app.Assemblies.Assemblies.OfType<LoadedModule>().Count() >= 2;
        yield return Hold(1);
        var modules = _app.Assemblies.Assemblies.OfType<LoadedModule>().ToList();
        CompareAssemblies(_app, modules[0], modules[1], _screen!.Strings.ComparisonTitle(modules[0].Name));
        yield return Hold(0.5);
        yield return IsIdle;
        yield return Hold(1);
        Record();
        yield return Hold(1.5);
    }

    private IEnumerable<Func<bool>> BookmarksScene()
    {
        yield return HasAssembly;
        yield return Hold(1);
        var marks = new[]
        {
            ("SerializeObject(object", "Serialize entry point"),
            ("DeserializeObject(string)", "Deserialize entry point"),
            ("DeserializeObject<T>(string)", "Deserialize to a type"),
        };
        foreach (var (member, name) in marks)
        {
            foreach (var wait in Show(Find("Newtonsoft.Json", "JsonConvert", member)))
                yield return wait;
            MarkFirstReturn(name);
        }
        OpenBookmarks(_screen!);
        foreach (var wait in Show(Find("Newtonsoft.Json", "JsonConvert", "ToString(bool")))
            yield return wait;
        yield return Hold(0.5);
        Record();
        yield return Hold(1);
        MarkFirstReturn("Booleans as JSON");
        yield return Hold(1.2);
        GoToBookmark(_app, _app.Bookmarks.Items[0]);
        yield return Hold(0.3);
        yield return IsIdle;
        yield return Hold(1.2);
    }

    private void MarkFirstReturn(string name)
    {
        var tab = _app.Tabs.Active!;
        var lines = tab.Document.Lines;
        var line = Enumerable.Range(0, lines.Length).First(i => lines[i].Text.TrimStart().StartsWith("return", StringComparison.Ordinal));
        ToggleBookmark(_app, tab, line, _ => name);
    }

    /// <summary>Gives the group holding the analyzer the share of the height its results need.</summary>
    private static bool GiveTheAnalyzerRoom(DockNode node)
    {
        if (node is not DockSplit split)
            return false;
        if (split.Second is DockGroup group && group.Panels.Contains(DockPanels.Analyzer))
        {
            split.Ratio = AnalyzerRatio;
            return true;
        }
        return GiveTheAnalyzerRoom(split.First) || GiveTheAnalyzerRoom(split.Second);
    }

    /// <summary>Waits until <paramref name="count"/> more frames are saved, so that each state is on film.</summary>
    private Func<bool> Frames(int count)
    {
        var until = _frames.Count + count;
        return () => _frames.Count >= until;
    }

    private IEnumerable<Func<bool>> GoTo()
    {
        yield return HasAssembly;
        yield return Hold(1);
        foreach (var wait in Show(Find("Newtonsoft.Json", "JsonSerializer", "CreateDefault()")))
            yield return wait;
        yield return Hold(0.5);
        Record();
        yield return Frames(1);
        var omnibar = _screen!.Omnibar;
        SearchInOmnibar(_screen);
        yield return Frames(1);
        foreach (var keys in new[] { "Js", "on", "Co", "nv", "ert" })
        {
            Type(keys);
            yield return Frames(1);
        }
        yield return () => omnibar.Search.ShowsQuery && !omnibar.Search.IsBusy;
        yield return Frames(2);
        omnibar.StopSearch();
        var session = _app.Session;
        var module = _app.Assemblies.Assemblies.OfType<LoadedModule>().First();
        var space = session.ChildrenOf(session.RootOf(module)).First(n => n.Text == "Newtonsoft.Json");
        foreach (var wait in Show(session.ChildrenOf(space).First(n => n.Text == "JsonConvert")))
            yield return wait;
        yield return Frames(2);
    }

    private IEnumerable<Func<bool>> NuGet()
    {
        yield return HasAssembly;
        yield return Hold(1);
        var picker = _screen!.NuGetPicker;
        Record();
        yield return Frames(1);
        foreach (var wait in ShowMenu(() => OpensFileMenu = true))
            yield return wait;
        picker.Open();
        yield return Frames(1);
        foreach (var keys in new[] { "Newt", "onso", "ft.J", "son" })
        {
            Type(keys);
            yield return Frames(1);
        }
        picker.SearchNow();
        yield return () => !picker.IsSearching && picker.Results.Count > 0;
        yield return Frames(1);
        var package = picker.Results[0];
        picker.Selection.Select(package, picker.Results);
        yield return () => picker.CanOpen;
        yield return Frames(2);
        picker.Download();
        yield return Frames(1);
        yield return () => !picker.IsOpen;
        yield return () => _app.Assemblies.Assemblies.Count >= 2;
        yield return Hold(1);
        yield return Frames(1);
        var session = _app.Session;
        var node = session.RootOf(_app.Assemblies.Assemblies.Last());
        foreach (var name in new[] { "Newtonsoft.Json", "JsonConvert" })
            node = session.ChildrenOf(node).First(n => n.Text == name);
        foreach (var wait in Show(node))
            yield return wait;
        yield return Frames(2);
    }

    private IEnumerable<Func<bool>> Export()
    {
        yield return HasAssembly;
        yield return Hold(1);
        foreach (var wait in Show(Find("System", "String", "IsNullOrEmpty(")))
            yield return wait;
        yield return Hold(0.5);
        Record();
        yield return Frames(1);
        foreach (var wait in ShowMenu(() => OpensFileMenu = true))
            yield return wait;
        var setup = _screen!.ExportSetup;
        setup.Open(_app.Assemblies.Assemblies, _app.Assemblies.Assemblies.OfType<LoadedModule>().First());
        setup.Folder = ExportFolder;
        yield return Frames(2);
        StartExport(_app, setup.CreateRequest());
        setup.Close();
        OpenExports(_screen);
        yield return () => _app.FinishedExports.Count > 0;
        yield return Frames(2);
    }

    /// <summary>Has popups and dialogs drawn in the main window instead of windows of their own, which
    /// reading the main window's frame would miss. The host has no public way to ask for that, and this
    /// branch leaves the framework as it is, so these two names are set by reflection; it fails loudly
    /// if the framework renames them.</summary>
    private static void DrawEverythingInTheMainWindow(ImmediateGuiAppHost<UiState> host)
    {
        foreach (var name in new[] { "_popupWindowsUnavailable", "_dialogWindowsUnavailable" })
        {
            var field = host.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new InvalidOperationException($"The host has no field called {name}.");
            field.SetValue(host, true);
        }
    }

    /// <summary>Types <paramref name="keys"/> as a person does, by way of the window, so that the
    /// focused field moves its caret and reacts as it would to the keyboard.</summary>
    private static void Type(string keys)
    {
        var window = Process.GetCurrentProcess().MainWindowHandle;
        foreach (var key in keys)
            PostMessage(window, CharacterMessage, key, 0);
    }

    /// <summary>Opens the File menu as a press on its title does. The bar has no public way to be
    /// told, and this branch leaves the framework as it is, so this goes by reflection and fails
    /// loudly if the framework renames what it uses. Recorded inside the bar, before its titles.</summary>
    public static void OpenFileMenu(Ui<UiState> ui)
    {
        if (!OpensFileMenu)
            return;
        OpensFileMenu = false;
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
        var find = typeof(UiWidgets).GetMethod("FindMenuBar", Hidden)
            ?? throw new InvalidOperationException("The widgets have no FindMenuBar.");
        var bar = find.Invoke(null, [ui])
            ?? throw new InvalidOperationException("There is no menu bar to open.");
        var open = bar.GetType().GetField("Open")
            ?? throw new InvalidOperationException("The menu bar has no Open.");
        open.SetValue(bar, ui.GetId("file"));
    }

    /// <summary>Presses Escape, which closes an open menu.</summary>
    private static void PressEscape()
    {
        var window = Process.GetCurrentProcess().MainWindowHandle;
        PostMessage(window, KeyDownMessage, EscapeKey, 0);
        PostMessage(window, KeyUpMessage, EscapeKey, 0);
    }

    /// <summary>Shows a menu for the frames it takes to be seen, then closes it.</summary>
    private IEnumerable<Func<bool>> ShowMenu(Action open)
    {
        open();
        yield return Frames(2);
        PressEscape();
        yield return Frames(1);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
}
