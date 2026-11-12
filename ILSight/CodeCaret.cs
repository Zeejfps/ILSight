using ZGF.Gui;
using ILSight.Core;

namespace ILSight;

/// <summary>Where the keys move a code view's caret to, over the rows its folds leave: a collapsed fold is a
/// row of its own, its first line, and the lines it hides are stepped over.</summary>
internal static class CodeCaret
{
    /// <summary>One character left of <paramref name="at"/>, or a word with <paramref name="byWord"/>; from the
    /// start of a line, the end of the row above.</summary>
    public static TextPosition StepLeft(DecompiledDocument document, CodeFolds folds, TextPosition at, bool byWord)
    {
        if (at.Column > 0)
        {
            var text = document.Lines[at.Line].Text;
            var column = Math.Min(at.Column, text.Length);
            return at with { Column = byWord ? TextBoundaries.PrevWord(text, column, splitPunctuation: true) : TextBoundaries.Prev(text, column) };
        }
        var row = CodeFolding.GetRowOf(folds, at.Line);
        if (row.Index <= 0)
        {
            return at;
        }

        var above = CodeFolding.GetLineAt(folds, new CodeRow(row.Index - 1));
        return new TextPosition(above, document.Lines[above].Text.Length);
    }

    /// <summary>One character right of <paramref name="at"/>, or a word with <paramref name="byWord"/>; from the
    /// end of a line, the start of the row below.</summary>
    public static TextPosition StepRight(DecompiledDocument document, CodeFolds folds, TextPosition at, bool byWord)
    {
        var text = document.Lines[at.Line].Text;
        if (at.Column < text.Length)
        {
            return at with { Column = byWord ? TextBoundaries.NextWord(text, at.Column, splitPunctuation: true) : TextBoundaries.Next(text, at.Column) };
        }

        var next = CodeFolding.GetNextLine(folds, at.Line);
        if (next >= document.Lines.Length)
        {
            return at;
        }

        return new TextPosition(next, 0);
    }

    /// <summary><paramref name="rows"/> rows below <paramref name="at"/>, above when negative, at the column of
    /// <paramref name="goal"/> or the end of the line when it is shorter; past the first row the start of the
    /// document, past the last the end of its line.</summary>
    public static TextPosition MoveRows(DecompiledDocument document, CodeFolds folds, TextPosition at, int rows, int goal)
    {
        if (folds.RowCount == 0)
        {
            return at;
        }

        var target = CodeFolding.GetRowOf(folds, at.Line).Index + rows;
        if (target < 0)
        {
            return new TextPosition(CodeFolding.GetLineAt(folds, new CodeRow(0)), 0);
        }

        if (target >= folds.RowCount)
        {
            var last = CodeFolding.GetLineAt(folds, new CodeRow(folds.RowCount - 1));
            return new TextPosition(last, document.Lines[last].Text.Length);
        }
        var line = CodeFolding.GetLineAt(folds, new CodeRow(target));
        var text = document.Lines[line].Text;
        return new TextPosition(line, TextBoundaries.Snap(text, Math.Min(goal, text.Length)));
    }

    /// <summary>Where Home goes from <paramref name="at"/>: the first character of its line that is not a space,
    /// or the start of the line when the caret is there already or the line is blank.</summary>
    public static TextPosition GetLineStart(DecompiledDocument document, TextPosition at)
    {
        var text = document.Lines[at.Line].Text;
        var indent = 0;
        while (indent < text.Length && char.IsWhiteSpace(text[indent]))
        {
            indent++;
        }

        if (indent == text.Length || at.Column == indent)
        {
            return at with { Column = 0 };
        }

        return at with { Column = indent };
    }

    public static TextPosition GetLineEnd(DecompiledDocument document, TextPosition at)
    {
        return at with { Column = document.Lines[at.Line].Text.Length };
    }

    /// <summary>The end of the document's last row, the end of a collapsed fold's first line when that is the last.</summary>
    public static TextPosition GetEnd(DecompiledDocument document, CodeFolds folds)
    {
        if (folds.RowCount == 0)
        {
            return default;
        }

        var last = CodeFolding.GetLineAt(folds, new CodeRow(folds.RowCount - 1));
        return new TextPosition(last, document.Lines[last].Text.Length);
    }
}
