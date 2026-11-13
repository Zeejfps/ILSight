using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.CodeViewArea;

namespace ILSight.App.Tests;

public sealed class CodeCaretTests
{
    private static DecompiledDocument Folded()
    {
        var builder = new DocumentBuilder { Language = DecompilationLanguage.CSharp };
        builder.Write("void F()", SpanKind.Plain);
        builder.WriteLine();
        builder.MarkFoldStart(isDefinition: true);
        builder.Write("{", SpanKind.Plain);
        builder.WriteLine();
        builder.Write("    Call(one);", SpanKind.Plain);
        builder.WriteLine();
        builder.Write("    Go();", SpanKind.Plain);
        builder.WriteLine();
        builder.Write("}", SpanKind.Plain);
        builder.MarkFoldEnd();
        builder.WriteLine();
        builder.Write("int x;", SpanKind.Plain);
        return builder.BuildDocument("folded");
    }

    private static CodeFolds FoldsOf(DecompiledDocument document, bool collapsed)
    {
        var folds = new CodeFolds();
        CodeFolding.Reset(folds, document);
        CodeFolding.SetAll(folds, collapsed);
        return folds;
    }

    [Fact]
    public void Left_and_right_cross_line_ends_and_step_over_a_collapsed_fold()
    {
        var document = Folded();
        var folds = FoldsOf(document, collapsed: true);

        Assert.Equal(
            new TextPosition { Line = 5, Column = 0 },
            CodeCaret.StepRight(document, folds, new TextPosition { Line = 0, Column = 8 }, byWord: false));
        Assert.Equal(
            new TextPosition { Line = 0, Column = 8 },
            CodeCaret.StepLeft(document, folds, new TextPosition { Line = 5, Column = 0 }, byWord: false));
        Assert.Equal(
            new TextPosition { Line = 0, Column = 0 },
            CodeCaret.StepLeft(document, folds, new TextPosition { Line = 0, Column = 0 }, byWord: false));
        Assert.Equal(
            new TextPosition { Line = 5, Column = 6 },
            CodeCaret.StepRight(document, folds, new TextPosition { Line = 5, Column = 6 }, byWord: false));
    }

    [Fact]
    public void Word_steps_stop_at_the_ends_of_names()
    {
        var document = Folded();
        var folds = FoldsOf(document, collapsed: false);

        Assert.Equal(
            new TextPosition { Line = 2, Column = 8 },
            CodeCaret.StepRight(document, folds, new TextPosition { Line = 2, Column = 4 }, byWord: true));
        Assert.Equal(
            new TextPosition { Line = 2, Column = 4 },
            CodeCaret.StepLeft(document, folds, new TextPosition { Line = 2, Column = 8 }, byWord: true));
    }

    [Fact]
    public void Rows_keep_the_goal_column_and_a_collapsed_fold_is_one_row()
    {
        var document = Folded();
        var open = FoldsOf(document, collapsed: false);
        var shut = FoldsOf(document, collapsed: true);

        Assert.Equal(
            new TextPosition { Line = 2, Column = 10 },
            CodeCaret.MoveRows(document, open, new TextPosition { Line = 3, Column = 7 }, -1, 10));
        Assert.Equal(
            new TextPosition { Line = 1, Column = 1 },
            CodeCaret.MoveRows(document, open, new TextPosition { Line = 2, Column = 10 }, -1, 10));
        Assert.Equal(
            new TextPosition { Line = 5, Column = 3 },
            CodeCaret.MoveRows(document, shut, new TextPosition { Line = 0, Column = 3 }, 1, 3));
        Assert.Equal(
            new TextPosition { Line = 0, Column = 0 },
            CodeCaret.MoveRows(document, shut, new TextPosition { Line = 0, Column = 3 }, -1, 3));
        Assert.Equal(
            new TextPosition { Line = 5, Column = 6 },
            CodeCaret.MoveRows(document, shut, new TextPosition { Line = 5, Column = 1 }, 1, 1));
    }

    [Fact]
    public void Home_goes_to_the_indent_then_to_the_start_of_the_line()
    {
        var document = Folded();

        Assert.Equal(
            new TextPosition { Line = 2, Column = 4 },
            CodeCaret.GetLineStart(document, new TextPosition { Line = 2, Column = 9 }));
        Assert.Equal(
            new TextPosition { Line = 2, Column = 0 },
            CodeCaret.GetLineStart(document, new TextPosition { Line = 2, Column = 4 }));
        Assert.Equal(
            new TextPosition { Line = 2, Column = 14 },
            CodeCaret.GetLineEnd(document, new TextPosition { Line = 2, Column = 4 }));
    }

    [Fact]
    public void The_end_of_a_document_ending_in_a_collapsed_fold_is_the_end_of_its_first_line()
    {
        var builder = new DocumentBuilder { Language = DecompilationLanguage.CSharp };
        builder.MarkFoldStart(isDefinition: true);
        builder.Write("{", SpanKind.Plain);
        builder.WriteLine();
        builder.Write("}", SpanKind.Plain);
        builder.MarkFoldEnd();
        var document = builder.BuildDocument("fold");

        Assert.Equal(
            new TextPosition { Line = 0, Column = 1 },
            CodeCaret.GetEnd(document, FoldsOf(document, collapsed: true)));
        Assert.Equal(
            new TextPosition { Line = 1, Column = 1 },
            CodeCaret.GetEnd(document, FoldsOf(document, collapsed: false)));
    }

    [Fact]
    public void The_arrows_move_the_caret_and_shift_extends_the_selection()
    {
        var view = CodeViewDriver.Showing(CodeViewDriver.Numbered(5), frames: 2);
        view.Click(1, 2);

        view.Key(KeyboardKey.DownArrow).Frame();
        view.Key(KeyboardKey.RightArrow).Frame();
        Assert.Equal(new TextPosition { Line = 2, Column = 3 }, view.State.Selection.Caret);
        Assert.False(view.State.HasSelection);

        view.Key(KeyboardKey.RightArrow, KeyModifiers.Shift).Frame();
        view.Key(KeyboardKey.RightArrow, KeyModifiers.Shift).Frame();
        Assert.Equal(new TextPosition { Line = 2, Column = 3 }, view.State.Selection.Start);
        Assert.Equal(new TextPosition { Line = 2, Column = 5 }, view.State.Selection.End);

        view.Key(KeyboardKey.LeftArrow).Frame();
        Assert.Equal(new TextPosition { Line = 2, Column = 3 }, view.State.Selection.Caret);
        Assert.False(view.State.HasSelection);
    }

    [Fact]
    public void A_focused_view_draws_the_caret_and_it_blinks_off()
    {
        var view = CodeViewDriver.Showing(CodeViewDriver.Numbered(5), frames: 2);
        view.Click(2, 3);
        view.Run();
        var caret = view.State.Frame.Caret;
        Assert.Equal(new TextPosition { Line = 2, Column = 3 }, caret);

        view.Frames.Wait(0.6);
        view.Run();

        Assert.Null(view.State.Frame.Caret);
    }

    [Fact]
    public void Moving_the_caret_past_the_bottom_of_the_view_scrolls_it_into_view()
    {
        var view = CodeViewDriver.Showing(CodeViewDriver.Numbered(200), frames: 2);
        view.Click(0, 0);

        for (var i = 0; i < 60; i++)
        {
            view.Key(KeyboardKey.DownArrow);
        }

        view.Run();

        Assert.Equal(60, view.State.Selection.Caret.Line);
        Assert.Contains(view.DrawnText(), d => d.Text == "line 60");
        Assert.True(view.State.ScrollY > 0f);
    }

    [Fact]
    public void Ctrl_with_the_document_end_key_goes_to_the_last_line()
    {
        var view = CodeViewDriver.Showing(CodeViewDriver.Numbered(200), frames: 2);
        view.Click(0, 0);

        view.Key(Shortcut.Command(KeyboardKey.End)).Frame();

        Assert.Equal(new TextPosition { Line = 199, Column = 8 }, view.State.Selection.Caret);
        Assert.Contains(view.DrawnText(), d => d.Text == "line 199");
    }
}
