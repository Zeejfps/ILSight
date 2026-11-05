using System.Diagnostics;
using System.Globalization;
using ILSight.Core;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using static ILSight.Bookmarks;
using static ILSight.Comparisons;
using static ILSight.Diagrams;
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
    private const string GoFile = "go";
    private const string TimesFile = "times.txt";

    /// <summary>Asks the diagram on screen to fit, as its Fit to view button does.</summary>
    public static bool FitDiagram;

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
            yield return Hold(0.5);
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
        yield return Hold(1);
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
        CompareAssemblies(_app, modules[0], modules[1], "Newtonsoft.Json 9.0 → 13.0");
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
}
