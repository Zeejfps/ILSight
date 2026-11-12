using ZGF.Desktop;
using ZGF.Gui.Immediate.Testing;
using ZGF.Gui.Immediate.Widgets;
using ZGF.Gui.Immediate;
using ILSight.App.Tests.Fixtures;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.CodeView;

namespace ILSight.App.Tests;

/// <summary>Runs a code view filling a window, with the synthetic metrics of <see cref="UiFrames"/>:
/// cells 8 by 16 centered in rows of the theme's code line height, the view's top-left at the window
/// padding (16, 16), and a gutter of a 16 wide bookmark column + 8 + digits + 8 (+ a 16 wide fold
/// column), then 12 before the text.</summary>
internal sealed class CodeViewDriver
{
    public const float Origin = 16f;
    public const float Cell = 8f;
    private const float CellHeight = 16f;
    private const float GutterPadding = 8f;
    private const float FoldColumn = 16f;
    private const float MarkColumn = 16f;
    private const float CodePadding = 12f;

    public static readonly float Row = UiTheme.Dark.CodeLineHeight;
    private static readonly float CellInset = MathF.Floor((Row - CellHeight) / 2);

    private static readonly Lazy<DecompiledDocument> s_greeter = new(() => Decompile(typeof(Greeter)));
    private static readonly Lazy<DecompiledDocument> s_outline = new(() => Decompile(typeof(Outline)));

    private readonly PostedActions _posted = PostedActions.OfThisTest();
    private readonly WorkflowRunner _workflows = new(new BackgroundWork(), new ErrorLog());

    /// <summary>A view of <paramref name="document"/>. What background work posts back, such as a
    /// tooltip's lookup, runs on the test's thread at the start of each frame, as the app runs it, and wakes the
    /// frames, so waiting for the UI runs the frame that takes it in.</summary>
    public CodeViewDriver(DecompiledDocument document, float height = 300f)
    {
        Document = document;
        Frames = new UiFrames(Screens.Blank(), height: height, body: Render) { FrameTime = 1f / 60f, BeforeFrame = _posted.Run };
        var ui = Frames.Run(0f, static ui => ui);
        _posted.WakeOnPost(ui.RequestFrame);
    }

    private void Render(Ui<UiState> ui)
    {
        using (ui.Screen())
        {
            Last = CodeViewWidget.RenderCodeView(ui, "code", Document, State, Bookmarks, tooltips: Tooltips, display: Display);
        }

        StartAskedLookups();
    }

    /// <summary>Starts looking up the tooltips the view asked for while it drew, as the app's frame edge does, over
    /// the assemblies they are of.</summary>
    private void StartAskedLookups()
    {
        var tooltips = Tooltips;
        if (tooltips is null || !tooltips.HasAsked)
        {
            return;
        }

        var handler = new LookUpTooltipHandler(new LookUpTooltipHandlerAdapter(tooltips.Assemblies));
        Commands.StartLookingUpTooltips(_workflows, handler, tooltips);
    }

    /// <summary>A driver that has run <paramref name="frames"/> frames of <paramref name="document"/>.</summary>
    public static CodeViewDriver Showing(DecompiledDocument document, float height = 300f, int frames = 1)
    {
        var view = new CodeViewDriver(document, height);
        view.Run(frames);
        return view;
    }

    public UiFrames Frames { get; }
    public CodeViewState State { get; } = new();
    public DecompiledDocument Document { get; set; }
    public IReadOnlyDictionary<int, bool>? Bookmarks { get; set; }
    public CodeTooltips? Tooltips { get; set; }
    public DisplayOptions? Display { get; set; }
    public CodeViewResult Last { get; private set; }

    private float FoldLeft => Origin + MarkColumn + GutterPadding + Digits * Cell + GutterPadding;

    private int Digits
    {
        get
        {
            var display = Display;
            if (display is not null && !display.ShowLineNumbers)
            {
                return 0;
            }

            return Math.Max(3, CodeTextDigits(Document.Lines.Length));
        }
    }

    public float TextLeft => FoldLeft + (Document.Folds.Length > 0 ? FoldColumn : 0f) + CodePadding;

    /// <summary>Where the bookmark mark of <paramref name="row"/> is, the view scrolled to the top.</summary>
    public (float X, float Y) MarkOf(CodeRow row)
    {
        return (Origin + (MarkColumn + GutterPadding) / 2, MiddleOf(row));
    }

    /// <summary>Where the fold chevron of <paramref name="row"/> is, the view scrolled to the top.</summary>
    public (float X, float Y) ChevronOf(CodeRow row)
    {
        return (FoldLeft + FoldColumn / 2, MiddleOf(row));
    }

    private static float MiddleOf(CodeRow row)
    {
        return Origin + CodeViewWidget.GetRowTop(row, Row) + Row / 2;
    }

    public CodeViewResult Run(int frames = 1)
    {
        Frames.Frame(frames);
        return Last;
    }

    /// <summary>Where <paramref name="column"/> of <paramref name="row"/> is, the view scrolled to the top.</summary>
    public (float X, float Y) PointOf(CodeRow row, int column)
    {
        return (TextLeft + column * Cell + 1f, MiddleOf(row));
    }

    /// <summary>Where <paramref name="column"/> of the row <paramref name="line"/> is shown on is, the view scrolled to the top.</summary>
    public (float X, float Y) PointOf(int line, int column)
    {
        return PointOf(CodeFolding.GetRowOf(State.Folds, line), column);
    }

    public UiFrames MoveTo(CodeRow row, int column)
    {
        var (x, y) = PointOf(row, column);
        return Frames.MovePointer(x, y).Frame();
    }

    public UiFrames MoveTo(int line, int column)
    {
        return MoveTo(CodeFolding.GetRowOf(State.Folds, line), column);
    }

    /// <summary>Clicks at the cell, as <see cref="Click(ValueTuple{float, float}, PointerButton, KeyModifiers)"/>.</summary>
    public CodeViewResult Click(CodeRow row, int column, PointerButton button = PointerButton.Left, KeyModifiers modifiers = KeyModifiers.None)
    {
        return Click(PointOf(row, column), button, modifiers);
    }

    /// <summary>Clicks at <paramref name="column"/> of the row <paramref name="line"/> is shown on.</summary>
    public CodeViewResult Click(int line, int column, PointerButton button = PointerButton.Left, KeyModifiers modifiers = KeyModifiers.None)
    {
        return Click(CodeFolding.GetRowOf(State.Folds, line), column, button, modifiers);
    }

    /// <summary>Clicks at <paramref name="at"/> and returns what the press asked for, or else what the release did.</summary>
    public CodeViewResult Click((float X, float Y) at, PointerButton button = PointerButton.Left, KeyModifiers modifiers = KeyModifiers.None)
    {
        Frames.MovePointer(at.X, at.Y).Frame();
        Frames.Press(button, modifiers).Frame();
        var pressed = Last;
        Frames.Release(button, modifiers).Frame();
        return pressed != default ? pressed : Last;
    }

    public UiFrames Key(KeyboardKey key, KeyModifiers modifiers = KeyModifiers.None)
    {
        return Frames.Key(key, modifiers).Frame();
    }

    public UiFrames Key(Shortcut shortcut)
    {
        return Frames.Key(shortcut).Frame();
    }

    /// <summary>The lines whose text was drawn, by the top of the row they were drawn in.</summary>
    public IEnumerable<(string Text, float Top)> DrawnText()
    {
        return Frames.Canvas.GlyphRuns.Where(r => r.Origin.X >= TextLeft - 0.5f).Select(r => (r.Text, RowTop(r.Origin.Y)));
    }

    /// <summary>The top of the row a glyph run drawn at canvas height <paramref name="y"/> sits in.</summary>
    public float RowTop(float y)
    {
        return Frames.Height - y - CellInset;
    }

    public static DecompiledDocument Numbered(int count, string prefix = "line ")
    {
        return Documents.CreateFromText("numbered", string.Join('\n', Enumerable.Range(0, count).Select(i => prefix + i)));
    }

    public static DecompiledDocument Greeter()
    {
        return s_greeter.Value;
    }

    public static DecompiledDocument Outline()
    {
        return s_outline.Value;
    }

    /// <summary>A document of <paramref name="type"/> decompiled through an assembly list of its own.</summary>
    public static DecompiledDocument Decompile(Type type)
    {
        var assembly = new AssemblyList().Open(type.Assembly.Location);
        var node = assembly.Root.Child(type.Namespace!, type.Name);
        return node.Decompile(DecompilationLanguage.CSharp);
    }

    private static int CodeTextDigits(int value)
    {
        return value.ToString().Length;
    }
}
