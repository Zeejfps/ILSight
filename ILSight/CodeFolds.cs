using ILSight.Core;

namespace ILSight;

/// <summary>A row of a code view, counted from the top of the document as its folds leave it: with a fold
/// collapsed above it, not the number of the line it shows.</summary>
internal readonly record struct CodeRow(int Index);

/// <summary>Which of a document's <see cref="DecompiledDocument.Folds"/> are collapsed, and the rows
/// that leaves: row r shows line <see cref="CodeFolding.GetLineAt"/>(r). <see cref="Rows"/>,
/// <see cref="IsIdentity"/> and <see cref="RowCount"/> follow <see cref="Collapsed"/>: change it only through
/// <see cref="CodeFolding"/>, which rebuilds them when either changes, never per frame.</summary>
internal sealed class CodeFolds
{
    public DecompiledDocument Document = DecompiledDocument.Empty;

    /// <summary>Whether each of the document's folds is collapsed.</summary>
    public bool[] Collapsed = [];

    /// <summary>The line each row shows, while <see cref="IsIdentity"/> is false; it may be longer than
    /// <see cref="RowCount"/>.</summary>
    public int[] Rows = [];

    /// <summary>Whether no fold is collapsed, so that every row shows the line of its own number.</summary>
    public bool IsIdentity = true;

    public int RowCount;

    public bool AnyCollapsed => !IsIdentity;
}

/// <summary>What is done to a document's <see cref="CodeFolds"/>. Most of this runs per row, so none of it
/// may allocate.</summary>
internal static class CodeFolding
{
    public static void Reset(CodeFolds state, DecompiledDocument document)
    {
        state.Document = document;
        var folds = document.Folds;
        state.Collapsed = folds.Length == 0 ? [] : new bool[folds.Length];
        for (var i = 0; i < folds.Length; i++)
            state.Collapsed[i] = folds[i].DefaultCollapsed;
        Rebuild(state);
    }

    public static bool IsCollapsed(CodeFolds state, int fold)
    {
        return state.Collapsed[fold];
    }

    public static void SetCollapsed(CodeFolds state, int fold, bool collapsed)
    {
        if (state.Collapsed[fold] == collapsed) return;
        state.Collapsed[fold] = collapsed;
        Rebuild(state);
    }

    public static void SetAll(CodeFolds state, bool collapsed)
    {
        Array.Fill(state.Collapsed, collapsed);
        Rebuild(state);
    }

    /// <summary>Expands every fold that hides <paramref name="line"/>; returns whether any did.</summary>
    public static bool Reveal(CodeFolds state, int line)
    {
        if (state.IsIdentity) return false;
        var folds = state.Document.Folds;
        var changed = false;
        for (var i = 0; i < folds.Length && folds[i].StartLine < line; i++)
        {
            if (state.Collapsed[i] && line <= folds[i].EndLine)
            {
                state.Collapsed[i] = false;
                changed = true;
            }
        }
        if (changed) Rebuild(state);
        return changed;
    }

    /// <summary>The collapsed fold whose row is <paramref name="line"/>, or null.</summary>
    public static int? FindCollapsedAt(CodeFolds state, int line)
    {
        if (state.IsIdentity) return null;
        var fold = Documents.FindFoldStartingAt(state.Document, line);
        if (fold == null) return null;
        return state.Collapsed[fold.Value] ? fold : null;
    }

    /// <summary>The innermost fold around the visible line <paramref name="line"/> is shown on, of
    /// any state or only <paramref name="collapsed"/> ones; null when there is none.</summary>
    public static int? FindInnermost(CodeFolds state, int line, bool? collapsed = null)
    {
        line = FindVisibleLine(state, line);
        var folds = state.Document.Folds;
        int? found = null;
        for (var i = 0; i < folds.Length && folds[i].StartLine <= line; i++)
        {
            if (line <= folds[i].EndLine && (collapsed == null || state.Collapsed[i] == collapsed))
                found = i;
        }
        return found;
    }

    /// <summary>The line <paramref name="row"/>, one of the rows, shows.</summary>
    public static int GetLineAt(CodeFolds state, CodeRow row)
    {
        return state.IsIdentity ? row.Index : state.Rows[row.Index];
    }

    /// <summary>Whether <paramref name="row"/> is one of the rows.</summary>
    public static bool HasRow(CodeFolds state, CodeRow row)
    {
        return row.Index >= 0 && row.Index < state.RowCount;
    }

    /// <summary>The row nearest <paramref name="row"/> that is one of them, the first while there are none.</summary>
    public static CodeRow ClampRow(CodeFolds state, CodeRow row)
    {
        return new CodeRow(Math.Clamp(row.Index, 0, Math.Max(0, state.RowCount - 1)));
    }

    /// <summary>The row <paramref name="line"/> is shown on: its own, or that of the fold hiding it.</summary>
    public static CodeRow FindRowOf(CodeFolds state, int line)
    {
        if (state.IsIdentity) return ClampRow(state, new CodeRow(line));
        int low = 0, high = state.RowCount - 1;
        while (low < high)
        {
            var mid = (low + high + 1) >> 1;
            if (state.Rows[mid] <= line) low = mid;
            else high = mid - 1;
        }
        return new CodeRow(low);
    }

    public static int FindVisibleLine(CodeFolds state, int line)
    {
        return state.RowCount == 0 ? 0 : GetLineAt(state, FindRowOf(state, line));
    }

    /// <summary>The first line after the row <paramref name="line"/> is shown on.</summary>
    public static int FindNextLine(CodeFolds state, int line)
    {
        var next = new CodeRow(FindRowOf(state, line).Index + 1);
        return HasRow(state, next) ? GetLineAt(state, next) : state.Document.Lines.Length;
    }

    private static void Rebuild(CodeFolds state)
    {
        var lines = state.Document.Lines.Length;
        var folds = state.Document.Folds;
        state.IsIdentity = Array.IndexOf(state.Collapsed, true) < 0;
        if (state.IsIdentity)
        {
            state.RowCount = lines;
            return;
        }

        if (state.Rows.Length < lines)
            state.Rows = new int[lines];
        var count = 0;
        var next = 0;
        for (var line = 0; line < lines;)
        {
            state.Rows[count++] = line;
            while (next < folds.Length && folds[next].StartLine < line)
                next++;
            line = next < folds.Length && folds[next].StartLine == line && state.Collapsed[next] ? folds[next].EndLine + 1 : line + 1;
        }
        state.RowCount = count;
    }
}
