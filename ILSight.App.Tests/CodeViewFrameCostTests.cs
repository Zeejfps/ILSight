using System.Diagnostics;
using Xunit.Abstractions;
using ILSight.Core;
using ILSight.CodeViewArea;

namespace ILSight.App.Tests;

public sealed class CodeViewFrameCostTests(ITestOutputHelper output)
{
    private const int Lines = 100_000;
    private const int WarmUpFrames = 60;
    private const int MeasuredFrames = 300;
    private const float Height = 900f;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void A_100k_line_document_costs_only_its_visible_lines_per_frame(bool scrolling, bool folded)
    {
        var view = new CodeViewDriver(folded ? Folded(Lines) : CodeViewDriver.Numbered(Lines), Height);
        if (folded)
        {
            view.Run();
            for (var f = 0; f < view.Document.Folds.Length; f += 2)
            {
                CodeViewWidget.SetCollapsed(view.State, f, true);
            }
        }
        view.Frames.MovePointer(200f, 200f);
        var visibleRows = (int)MathF.Ceiling((Height - 2 * CodeViewDriver.Origin) / CodeViewDriver.Row) + 1;

        for (var i = 0; i < WarmUpFrames; i++)
        {
            Frame(view, scrolling);
        }

        var maxElements = 0;
        var totalElements = 0L;
        for (var i = 0; i < MeasuredFrames; i++)
        {
            Frame(view, scrolling);
            var elements = Elements(view);
            maxElements = Math.Max(maxElements, elements);
            totalElements += elements;
        }
        var glyphRuns = view.Frames.Canvas.GlyphRuns.Count;

        view.Frames.Canvas.Recording = false;
        for (var i = 0; i < WarmUpFrames; i++)
        {
            Frame(view, scrolling);
        }

        var allocated = SteadyAllocations.PerFrame(MeasuredFrames, _ => Frame(view, scrolling));
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < MeasuredFrames; i++)
        {
            Frame(view, scrolling);
        }

        clock.Stop();

        output.WriteLine($"{(scrolling ? "scrolling" : "static")}{(folded ? ", half the folds collapsed" : "")}: " +
                         $"{clock.Elapsed.TotalMilliseconds / MeasuredFrames:F3} ms/frame, {allocated:N0} B/frame allocated, " +
                         $"{(double)totalElements / MeasuredFrames:F1} elements/frame (max {maxElements}), {glyphRuns} glyph runs, {visibleRows} rows in view");

        Assert.InRange(glyphRuns, visibleRows - 2, (folded ? 3 : 2) * visibleRows + 4);
        Assert.True(maxElements <= (folded ? 5 : 4) * visibleRows + 32, $"{maxElements} elements for {visibleRows} visible rows");
        Assert.Equal(0, allocated);
    }

    /// <summary>What the frames above never reach: the pointer, find and the caret each mark a row of the
    /// paint, so that a closure or a boxed value on any of those branches shows.</summary>
    [Theory]
    [InlineData("reference")]
    [InlineData("chevron")]
    [InlineData("chip")]
    [InlineData("mark")]
    [InlineData("find")]
    [InlineData("brackets")]
    public async Task A_frame_with_what_the_pointer_find_or_the_caret_marks_allocates_nothing(string marked)
    {
        var view = await MarkingAsync(marked);
        Assert.True(IsMarked(view, marked));

        view.Frames.Canvas.Recording = false;
        for (var i = 0; i < WarmUpFrames; i++)
        {
            view.Run();
        }

        var allocated = SteadyAllocations.PerFrame(MeasuredFrames, _ => view.Run());

        output.WriteLine($"{marked}: {allocated:N0} B/frame allocated");
        Assert.True(IsMarked(view, marked));
        Assert.Equal(0, allocated);
    }

    private static async Task<CodeViewDriver> MarkingAsync(string marked)
    {
        return marked switch
        {
            "reference" => await HoverReferenceAsync(),
            "chevron" => HoverChevron(),
            "chip" => HoverChip(),
            "mark" => HoverMark(),
            "find" => OpenFind(),
            "brackets" => await PutCaretOnBracketAsync(),
            _ => throw new UnreachableException(),
        };
    }

    private static bool IsMarked(CodeViewDriver view, string marked)
    {
        var frame = view.State.Frame;
        return marked switch
        {
            "reference" => frame.Highlight != null,
            "chevron" => frame.HoverFold is not null,
            "chip" => frame.HoverChip is not null,
            "mark" => frame.HoverMark is not null,
            "find" => frame.MatchLength > 0 && frame.CurrentMatch is not null,
            "brackets" => frame.Brackets is not null,
            _ => throw new UnreachableException(),
        };
    }

    private static async Task<CodeViewDriver> HoverReferenceAsync()
    {
        var view = CodeViewDriver.Showing(await CodeViewDriver.GreeterAsync(), Height, frames: 2);
        var (line, column) = GetReference(view.Document);
        view.MoveTo(line, column + 1);
        return view;
    }

    private static (int Line, int Column) GetReference(DecompiledDocument document)
    {
        for (var line = 0; line < document.Lines.Length; line++)
        {
            foreach (var span in document.Lines[line].Spans)
            {
                if (span.Reference != null)
                {
                    return (line, span.Start);
                }
            }
        }
        throw new UnreachableException();
    }

    private static CodeViewDriver HoverChevron()
    {
        var view = CodeViewDriver.Showing(Folded(100), Height, frames: 2);
        var (x, y) = view.ChevronOf(new CodeRow(view.Document.Folds[0].StartLine));
        view.Frames.MovePointer(x, y).Frame();
        return view;
    }

    private static CodeViewDriver HoverChip()
    {
        var view = CodeViewDriver.Showing(Folded(100), Height, frames: 2);
        var fold = view.Document.Folds[0];
        CodeViewWidget.SetCollapsed(view.State, 0, true);
        view.Run();
        view.MoveTo(fold.StartLine, view.Document.Lines[fold.StartLine].Text.Length + 2);
        return view;
    }

    private static CodeViewDriver HoverMark()
    {
        var view = CodeViewDriver.Showing(CodeViewDriver.Numbered(100), Height, frames: 2);
        view.Bookmarks = new Dictionary<int, bool> { [1] = true, [3] = false };
        var (x, y) = view.MarkOf(new CodeRow(2));
        view.Frames.MovePointer(x, y).Frame();
        return view;
    }

    private static CodeViewDriver OpenFind()
    {
        var view = CodeViewDriver.Showing(CodeViewDriver.Numbered(100), Height, frames: 2);
        view.Click(0, 0);
        view.Key(Shortcuts.Find);
        view.Frames.Type("line").Frame(3);
        return view;
    }

    private static async Task<CodeViewDriver> PutCaretOnBracketAsync()
    {
        var view = CodeViewDriver.Showing(await CodeViewDriver.GreeterAsync(), Height, frames: 2);
        view.Display = new DisplayOptions();
        var (line, column) = view.Document.GetPosition("{");
        view.Click(line, column);
        return view;
    }

    private static DecompiledDocument Folded(int lines)
    {
        var builder = new DocumentBuilder { Language = DecompilationLanguage.CSharp };
        for (var member = 0; member < lines / 10; member++)
        {
            builder.Write($"    public void Member{member}()", SpanKind.Plain);
            builder.WriteLine();
            builder.MarkFoldStart(isDefinition: true);
            builder.Write("    {", SpanKind.Plain);
            builder.WriteLine();
            for (var i = 0; i < 7; i++)
            {
                builder.Write($"        Call({member}, {i});", SpanKind.Plain);
                builder.WriteLine();
            }
            builder.Write("    }", SpanKind.Plain);
            builder.MarkFoldEnd();
            builder.WriteLine();
        }
        return builder.BuildDocument("folded");
    }

    private static void Frame(CodeViewDriver view, bool scrolling)
    {
        if (scrolling)
        {
            view.Frames.Wheel(-3);
        }

        view.Run();
    }

    private static int Elements(CodeViewDriver view)
    {
        var canvas = view.Frames.Canvas;
        return canvas.Rects.Count + canvas.Texts.Count + canvas.GlyphRuns.Count + canvas.Lines.Count + canvas.Images.Count +
               canvas.BoxShadows.Count + canvas.Circles.Count + canvas.Beziers.Count + canvas.CubicBeziers.Count;
    }
}
