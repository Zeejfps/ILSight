using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.CodeViewArea;

namespace ILSight.App.Tests;

public sealed class CodeViewFoldTests
{
    private static async Task<CodeViewDriver> OutlineAsync(float height = 1200f)
    {
        return CodeViewDriver.Showing(await CodeViewDriver.OutlineAsync(), height, frames: 2);
    }

    private static List<string> Numbers(CodeViewDriver view)
    {
        return view.Frames.Canvas.GlyphRuns.Where(r => r.Origin.X < view.TextLeft - 0.5f && char.IsDigit(r.Text[0]))
            .OrderByDescending(r => r.Origin.Y).Select(r => r.Text).ToList();
    }

    private static List<string> Rows(CodeViewDriver view)
    {
        return view.Frames.Canvas.GlyphRuns
            .Select(r => (r.Text, r.Origin.X, Top: view.RowTop(r.Origin.Y)))
            .Where(r => r.X >= view.TextLeft - 0.5f && r.Top >= CodeViewDriver.Origin - 0.5f)
            .GroupBy(r => r.Top).OrderBy(g => g.Key)
            .Select(g => string.Concat(g.OrderBy(r => r.X).Select(r => r.Text))).ToList();
    }

    /// <summary>Whether the view draws the fold starting at the row holding <paramref name="start"/> folded: that row
    /// with the chip standing for what the fold hides.</summary>
    private static bool ShowsFolded(CodeViewDriver view, string start)
    {
        return Rows(view).Single(r => r.Contains(start)).Contains('…');
    }

    private static void ClickChevron(CodeViewDriver view, CodeRow row)
    {
        view.Click(view.ChevronOf(row));
        view.Frames.Wait(1);
    }

    [Fact]
    public async Task The_usings_start_collapsed_behind_a_chip_and_line_numbers_stay_real()
    {
        var view = await OutlineAsync();
        var document = view.Document;

        var numbers = Numbers(view);
        var rows = Rows(view);

        var usings = document.FoldAt("using System.Collections.Generic;");
        Assert.Equal(["1", "2", (usings.StartLine + 1).ToString(), (usings.EndLine + 2).ToString()], numbers.Take(4));
        Assert.Equal("using System.Collections.Generic;…", rows[usings.StartLine]);
        Assert.DoesNotContain(rows, r => r.Contains("using System.Text;"));
    }

    [Fact]
    public async Task Clicking_a_chevron_folds_a_body_onto_its_signature_and_clicking_the_chip_unfolds_it()
    {
        var view = await OutlineAsync();
        var document = view.Document;
        var first = document.GetPosition("public int First()").Line;
        var row = CodeFolding.GetRowOf(view.State.Folds, first);

        var (x, y) = view.ChevronOf(row);
        view.Frames.MovePointer(x, y).Frame();
        var hand = view.Frames.Cursor;
        var lit = view.Frames.Canvas.Lines.Count(l => l.Inputs.Color == UiTheme.Dark.Text);
        ClickChevron(view, row);
        var folded = Rows(view);
        var numbers = Numbers(view);

        Assert.Equal(MouseCursor.Hand, hand);
        Assert.Equal(2, lit);
        Assert.Contains("    public int First(){ … }", folded);
        Assert.DoesNotContain(folded, r => r.Contains("int total = 0;"));
        Assert.Equal((first + 1).ToString(), numbers[row.Index]);
        Assert.Equal((document.FoldAt("public int First()").EndLine + 2).ToString(), numbers[row.Index + 1]);

        view.Click(row, document.Lines[first].Text.Length + 2);

        Assert.False(ShowsFolded(view, "public int First()"));
        Assert.Contains(Rows(view), r => r.Contains("int total = 0;"));
    }

    [Fact]
    public async Task Ctrl_m_m_toggles_the_fold_at_the_caret_and_ctrl_shift_brackets_collapse_and_expand()
    {
        var view = await OutlineAsync();
        var document = view.Document;
        var (line, column) = document.GetPosition("builder.Append");
        view.Click(line, column);

        view.Key(KeyboardKey.M, KeyModifiers.Control);
        view.Key(KeyboardKey.M, KeyModifiers.Control);
        var toggled = ShowsFolded(view, "public string Second()");
        view.Key(KeyboardKey.M, KeyModifiers.Control);
        view.Key(KeyboardKey.M);
        var back = ShowsFolded(view, "public string Second()");
        view.Key(Shortcuts.CollapseFold);
        var collapsed = ShowsFolded(view, "public string Second()");
        view.Key(Shortcuts.CollapseFold);
        var outer = ShowsFolded(view, "public class Outline");
        view.Key(Shortcuts.ExpandFold);
        view.Key(Shortcuts.ExpandFold);

        Assert.True(toggled);
        Assert.False(back);
        Assert.True(collapsed);
        Assert.True(outer);
        Assert.False(ShowsFolded(view, "public string Second()"));
        Assert.False(ShowsFolded(view, "public class Outline"));
    }

    [Fact]
    public async Task The_context_menu_collapses_and_expands_everything_and_toggles_the_fold_under_the_pointer()
    {
        var view = await OutlineAsync();
        var document = view.Document;

        void Pick(int line, string item)
        {
            view.Click(line, 2, PointerButton.Right);
            view.Run();
            var at = view.Frames.Canvas.Texts.Single(t => t.Inputs.Text == item).Inputs.Position;
            view.Frames.Click(at.Left + 2f, view.Frames.Height - at.Center.Y).Frame().Wait(1);
        }

        Pick(0, "Collapse all");
        var rowsCollapsed = Rows(view);
        Pick(0, "Expand all");
        var rowsExpanded = Rows(view);
        var third = document.GetPosition("public int Third()").Line;
        Pick(third, "Toggle fold");

        Assert.Equal(4, rowsCollapsed.Count);
        Assert.Equal("using System.Collections.Generic;…", rowsCollapsed[2]);
        Assert.Equal("public class Outline{ … }", rowsCollapsed[3]);
        Assert.Equal(document.Lines.Count(l => l.Text.Length > 0), rowsExpanded.Count);
        Assert.DoesNotContain(rowsExpanded, static r => r.Contains('…'));
        Assert.True(ShowsFolded(view, "public int Third()"));
    }

    [Fact]
    public async Task Copying_across_a_collapsed_fold_copies_what_it_hides()
    {
        var view = await OutlineAsync();
        var document = view.Document;
        var first = document.GetPosition("public int First()").Line;
        var end = document.FoldAt("public int First()").EndLine + 2;
        ClickChevron(view, CodeFolding.GetRowOf(view.State.Folds, first));

        view.MoveTo(first - 1, 0);
        view.Frames.Press().Frame();
        view.MoveTo(end, 4);
        view.Frames.Release().Frame();
        view.Key(Shortcuts.Copy);

        Assert.Equal(
            CodeText.Between(document, new TextPosition { Line = first - 1, Column = 0 }, new TextPosition { Line = end, Column = 4 }),
            view.Frames.Clipboard.Text);
        Assert.Contains("int total = 0;", view.Frames.Clipboard.Text);
    }

    [Fact]
    public async Task A_find_match_inside_a_collapsed_fold_expands_it_when_it_becomes_current()
    {
        var view = await OutlineAsync();
        var document = view.Document;
        var second = document.FoldIndexAt("public string Second()");
        CodeViewWidget.SetCollapsed(view.State, second, true);
        view.Run();
        view.Click(0, 0);

        view.Key(Shortcuts.Find);
        view.Frames.Type("needle").Frame(2);

        Assert.False(ShowsFolded(view, "public string Second()"));
        Assert.Contains(Rows(view), r => r.Contains("builder.Append(\"needle\")"));
        Assert.Equal(document.GetPosition("\"needle\"").Line, view.State.Selection.Start.Line);
    }

    [Fact]
    public async Task RevealLine_into_nested_collapsed_folds_expands_them_all()
    {
        var view = await OutlineAsync();
        var document = view.Document;
        var inner = document.FoldIndexAt("public class Inner");
        var deep = document.FoldIndexAt("public int Deep()");
        CodeViewWidget.SetCollapsed(view.State, deep, true);
        CodeViewWidget.SetCollapsed(view.State, inner, true);
        view.Run();

        CodeViewWidget.RevealLine(view.State, document.GetPosition("return 7;").Line);
        view.Run(2);

        Assert.False(ShowsFolded(view, "public class Inner"));
        Assert.False(ShowsFolded(view, "public int Deep()"));
        Assert.Contains(Rows(view), r => r.Contains("return 7;"));
        Assert.True(ShowsFolded(view, "using System.Collections.Generic;"));
    }

    [Fact]
    public async Task Folding_above_the_view_keeps_the_top_line_in_place()
    {
        var view = await OutlineAsync(height: 200f);
        var document = view.Document;
        var target = document.GetPosition("public string Second()").Line;
        CodeViewWidget.RestorePosition(view.State, new CodeViewPosition
        {
            TopLine = target,
            ScrollX = 0f,
            Anchor = default,
            Caret = default,
        });
        view.Run(2);
        var before = view.State.Position.TopLine;

        CodeViewWidget.SetCollapsed(view.State, document.FoldIndexAt("public int First()"), true);
        view.Run(2);

        Assert.Equal(target, before);
        Assert.Equal(target, view.State.Position.TopLine);
        Assert.StartsWith("    public string Second()", Rows(view)[0]);
    }

    [Fact]
    public async Task A_new_document_starts_from_its_own_default_folds()
    {
        var view = await OutlineAsync();
        CodeViewWidget.ExpandAll(view.State);
        view.Run();

        view.Document = await CodeViewDriver.DecompileAsync(typeof(Fixtures.Outline));
        view.Run();

        Assert.True(ShowsFolded(view, "using System.Collections.Generic;"));
        Assert.False(ShowsFolded(view, "public int First()"));
    }

    [Fact]
    public async Task The_rows_follow_what_is_collapsed_through_each_function_that_changes_it()
    {
        var document = await CodeViewDriver.OutlineAsync();
        var folds = new CodeFolds();
        CodeFolding.Reset(folds, document);
        var fold = document.Folds[0];

        CodeFolding.SetAll(folds, false);
        var allRows = folds.RowCount;
        var anyAfterExpanding = folds.AnyCollapsed;
        CodeFolding.SetCollapsed(folds, 0, true);
        var collapsedRows = folds.RowCount;
        var shownFor = CodeFolding.GetVisibleLine(folds, fold.EndLine);
        var revealed = CodeFolding.TryReveal(folds, fold.EndLine);

        Assert.False(anyAfterExpanding);
        Assert.Equal(document.Lines.Length, allRows);
        Assert.Equal(document.Lines.Length - (fold.EndLine - fold.StartLine), collapsedRows);
        Assert.Equal(fold.StartLine, shownFor);
        Assert.True(revealed);
        Assert.Equal(allRows, folds.RowCount);
        Assert.False(folds.AnyCollapsed);
    }

    [Fact]
    public async Task A_layout_stays_as_it_was_when_the_folds_change_after_it()
    {
        var document = await CodeViewDriver.OutlineAsync();
        var folds = new CodeFolds();
        CodeFolding.Reset(folds, document);
        CodeFolding.SetAll(folds, false);
        var before = folds.Layout;

        CodeFolding.SetCollapsed(folds, 0, true);

        Assert.NotSame(before, folds.Layout);
        Assert.False(before.Collapsed[0]);
        Assert.True(before.IsIdentity);
        Assert.Equal(document.Lines.Length, before.RowCount);
    }

    [Fact]
    public async Task Revealing_a_line_no_fold_hides_leaves_the_layout_as_it_is()
    {
        var document = await CodeViewDriver.OutlineAsync();
        var folds = new CodeFolds();
        CodeFolding.Reset(folds, document);
        CodeFolding.SetAll(folds, false);
        CodeFolding.SetCollapsed(folds, 0, true);
        var before = folds.Layout;

        var revealed = CodeFolding.TryReveal(folds, document.Folds[0].StartLine);

        Assert.False(revealed);
        Assert.Same(before, folds.Layout);
    }

    [Fact]
    public async Task The_rows_of_a_layout_are_only_the_rows_shown()
    {
        var document = await CodeViewDriver.OutlineAsync();
        var folds = new CodeFolds();
        CodeFolding.Reset(folds, document);
        CodeFolding.SetAll(folds, false);

        CodeFolding.SetCollapsed(folds, 0, true);

        Assert.Equal(folds.RowCount, folds.Rows.Length);
    }
}
