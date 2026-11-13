using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>A code view's find bar: whether it shows, whether its field is still to take focus, the matches of the
/// query last looked for, and the one selected, which is kept while the bar is closed, for F3 to go on from.</summary>
internal sealed class CodeFind
{
    private bool _isFocusPending;

    public CodeFindIndex Index { get; private set; } = CodeFindIndex.None;

    public bool IsOpen { get; private set; }

    /// <summary>The index of the match selected, or null.</summary>
    public int? CurrentMatch { get; private set; }

    /// <summary>Shows the bar, its field to take focus.</summary>
    public void OpenToType()
    {
        IsOpen = true;
        _isFocusPending = true;
    }

    /// <summary>Shows the bar, leaving focus where it is.</summary>
    public void Open()
    {
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
    }

    /// <summary>True once after <see cref="OpenToType"/>: the field should take focus.</summary>
    public bool TryTakeFocus()
    {
        var focus = _isFocusPending;
        _isFocusPending = false;
        return focus;
    }

    /// <summary>Looks for <paramref name="query"/> in <paramref name="document"/> again when either changed since
    /// it last did; false when neither did.</summary>
    public bool TryRescan(DecompiledDocument document, string query)
    {
        var index = Index;
        if (ReferenceEquals(document, index.Document) && string.Equals(query, index.Query, StringComparison.Ordinal))
        {
            return false;
        }

        Index = new CodeFindIndex { Matches = ScanForMatches(document, query), Document = document, Query = query };
        return true;
    }

    public void ClearMatch()
    {
        CurrentMatch = null;
    }

    /// <summary>Selects the first match at or after <paramref name="at"/>, wrapping to the first; none when there are
    /// none.</summary>
    public void SelectFirstMatchFrom(TextPosition at)
    {
        CurrentMatch = FindFirstMatch(Index, at);
    }

    /// <summary>Selects the next match, or the previous when <paramref name="direction"/> is negative, wrapping; with
    /// none selected, the first after <paramref name="caret"/>, or the last before it.</summary>
    public void Step(int direction, TextPosition caret)
    {
        var count = Index.Matches.Count;
        if (count == 0)
        {
            CurrentMatch = null;
            return;
        }
        var current = CurrentMatch;
        if (current is not null)
        {
            CurrentMatch = (current.Value + direction + count) % count;
        }
        else if (direction > 0)
        {
            CurrentMatch = FindFirstMatch(Index, caret);
        }
        else
        {
            CurrentMatch = (GetMatchOnOrAfter(Index, caret) - 1 + count) % count;
        }
    }

    private static List<TextPosition> ScanForMatches(DecompiledDocument document, string query)
    {
        var matches = new List<TextPosition>();
        if (query.Length == 0)
        {
            return matches;
        }

        var lines = document.Lines;
        for (var line = 0; line < lines.Length; line++)
        {
            var text = lines[line].Text;
            var at = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            while (at >= 0)
            {
                matches.Add(new TextPosition { Line = line, Column = at });
                at = text.IndexOf(query, at + query.Length, StringComparison.OrdinalIgnoreCase);
            }
        }
        return matches;
    }

    private static int? FindFirstMatch(CodeFindIndex find, TextPosition at)
    {
        if (find.Matches.Count == 0)
        {
            return null;
        }

        var index = GetMatchOnOrAfter(find, at);
        return index < find.Matches.Count ? index : 0;
    }

    // The count when no match is at or after it.
    private static int GetMatchOnOrAfter(CodeFindIndex find, TextPosition at)
    {
        int low = 0, high = find.Matches.Count;
        while (low < high)
        {
            var mid = (low + high) >> 1;
            if (find.Matches[mid] < at)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        return low;
    }
}
