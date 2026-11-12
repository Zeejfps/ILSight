using ILSight.Core;

namespace ILSight.CodeView;

/// <summary>What is done to a document's <see cref="CodeFolds"/>. Most of this runs per row, so none of it
/// may allocate.</summary>
internal static class CodeFolding
{
    public static void Reset(CodeFolds state, DecompiledDocument document)
    {
        var folds = document.Folds;
        var collapsed = new bool[folds.Length];
        for (var i = 0; i < folds.Length; i++)
        {
            collapsed[i] = folds[i].DefaultCollapsed;
        }

        state.Layout = CreateLayout(document, collapsed);
    }

    public static bool IsCollapsed(CodeFolds state, int fold)
    {
        return IsCollapsed(state.Layout, fold);
    }

    public static bool IsCollapsed(CodeFoldLayout layout, int fold)
    {
        return layout.Collapsed[fold];
    }

    public static void SetCollapsed(CodeFolds state, int fold, bool collapsed)
    {
        if (state.Collapsed[fold] == collapsed)
        {
            return;
        }

        var changed = state.Collapsed.ToArray();
        changed[fold] = collapsed;
        state.Layout = CreateLayout(state.Document, changed);
    }

    public static void SetAll(CodeFolds state, bool collapsed)
    {
        var changed = new bool[state.Collapsed.Length];
        Array.Fill(changed, collapsed);
        state.Layout = CreateLayout(state.Document, changed);
    }

    /// <summary>Expands every fold that hides <paramref name="line"/>; returns whether any did.</summary>
    public static bool TryReveal(CodeFolds state, int line)
    {
        if (state.IsIdentity)
        {
            return false;
        }

        var folds = state.Document.Folds;
        bool[]? opened = null;
        for (var i = 0; i < folds.Length && folds[i].StartLine < line; i++)
        {
            if (state.Collapsed[i] && line <= folds[i].EndLine)
            {
                opened ??= state.Collapsed.ToArray();
                opened[i] = false;
            }
        }
        if (opened is null)
        {
            return false;
        }

        state.Layout = CreateLayout(state.Document, opened);
        return true;
    }

    /// <summary>The collapsed fold whose row is <paramref name="line"/>, or null.</summary>
    public static int? FindCollapsedAt(CodeFolds state, int line)
    {
        if (state.IsIdentity)
        {
            return null;
        }

        var fold = Documents.FindFoldStartingAt(state.Document, line);
        if (fold is null)
        {
            return null;
        }

        return state.Collapsed[fold.Value] ? fold : null;
    }

    /// <summary>The innermost fold around the visible line <paramref name="line"/> is shown on, of
    /// any state or only <paramref name="collapsed"/> ones; null when there is none.</summary>
    public static int? FindInnermost(CodeFolds state, int line, bool? collapsed = null)
    {
        line = GetVisibleLine(state, line);
        var folds = state.Document.Folds;
        int? found = null;
        for (var i = 0; i < folds.Length && folds[i].StartLine <= line; i++)
        {
            if (line <= folds[i].EndLine && (collapsed is null || state.Collapsed[i] == collapsed))
            {
                found = i;
            }
        }
        return found;
    }

    /// <summary>The line <paramref name="row"/>, one of the rows, shows.</summary>
    public static int GetLineAt(CodeFolds state, CodeRow row)
    {
        return GetLineAt(state.Layout, row);
    }

    /// <summary>The line <paramref name="row"/>, one of the rows of <paramref name="layout"/>, shows.</summary>
    public static int GetLineAt(CodeFoldLayout layout, CodeRow row)
    {
        return layout.IsIdentity ? row.Index : layout.Rows[row.Index];
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
    public static CodeRow GetRowOf(CodeFolds state, int line)
    {
        if (state.IsIdentity)
        {
            return ClampRow(state, new CodeRow(line));
        }

        int low = 0, high = state.RowCount - 1;
        while (low < high)
        {
            var mid = (low + high + 1) >> 1;
            if (state.Rows[mid] <= line)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }
        return new CodeRow(low);
    }

    public static int GetVisibleLine(CodeFolds state, int line)
    {
        return state.RowCount == 0 ? 0 : GetLineAt(state, GetRowOf(state, line));
    }

    /// <summary>The first line after the row <paramref name="line"/> is shown on.</summary>
    public static int GetNextLine(CodeFolds state, int line)
    {
        var next = new CodeRow(GetRowOf(state, line).Index + 1);
        return HasRow(state, next) ? GetLineAt(state, next) : state.Document.Lines.Length;
    }

    private static CodeFoldLayout CreateLayout(DecompiledDocument document, bool[] collapsed)
    {
        var lines = document.Lines.Length;
        var folds = document.Folds;
        var firstCollapsed = Array.IndexOf(collapsed, true);
        if (firstCollapsed < 0)
        {
            return new CodeFoldLayout { Document = document, Collapsed = [..collapsed], Rows = [], IsIdentity = true, RowCount = lines };
        }

        var rows = new int[lines];
        var count = 0;
        var next = 0;
        for (var line = 0; line < lines;)
        {
            rows[count++] = line;
            while (next < folds.Length && folds[next].StartLine < line)
            {
                next++;
            }

            line = next < folds.Length && folds[next].StartLine == line && collapsed[next] ? folds[next].EndLine + 1 : line + 1;
        }
        return new CodeFoldLayout { Document = document, Collapsed = [..collapsed], Rows = [..rows.AsSpan(0, count)], IsIdentity = false, RowCount = count };
    }
}
