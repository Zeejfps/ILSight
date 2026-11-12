using ILSight.Core;

namespace ILSight;

/// <summary>Everything a <see cref="CodeViewWidget.CodeView"/> keeps between frames: scroll,
/// selection, folds, find. One per open tab, owned by the app.</summary>
public sealed class CodeViewState
{
    internal readonly CodeViewFrame Frame = new();
    internal CodeFindIndex Find = CodeFindIndex.None;

    /// <summary>The bracket at the caret or just before it and the one matching it, by the document and the
    /// caret they were looked up for; see <see cref="MatchingBrackets"/>.</summary>
    internal Memo<(ByReference<DecompiledDocument>, TextPosition), BracketPair?> Brackets;

    internal DecompiledDocument? Document;
    internal int MaxColumns;
    internal CodePress? Press;
    internal float ScrollX;
    internal float ScrollY;
    internal float RowHeight;
    internal float LaidOutScrollY;
    internal float LaidOutScrollX;
    internal CodeReference? Marked;
    internal CodeMenuTarget Menu = CodeMenuTarget.None;
    internal readonly KeptTooltip Tooltip = new();
    internal PendingScroll? Pending;
    internal CodeFlash? Flash;
    internal bool FindOpen;
    internal bool FocusFind;
    internal string Query = "";
    /// <summary>The index of the find match selected, or null; kept while find is closed, for F3 to go on from.</summary>
    internal int? CurrentMatch;
    internal int KeyScrollRows;
    /// <summary>How many rows Page Up and Page Down move, a view less one, as last laid out.</summary>
    internal int PageRows = 1;
    internal CaretGoal? Goal;
    /// <summary>When the caret, at <see cref="BlinkCaret"/>, last moved: it shows from then for half a blink.</summary>
    internal double BlinkFrom;
    internal TextPosition BlinkCaret;

    public CodeSelection Selection { get; } = new();

    internal CodeFolds Folds => Selection.Folds;

    /// <summary>Where the view stands now; where it is to be returned to while that waits for it to be drawn.</summary>
    public CodeViewPosition Position
    {
        get
        {
            if (Pending is PendingScroll.Restore restoring)
            {
                return restoring.Position;
            }

            var top = RowHeight > 0f && Folds.RowCount > 0 ? CodeFolding.GetLineAt(Folds, CodeFolding.ClampRow(Folds, CodeViewWidget.GetRowAt(ScrollY, RowHeight))) : 0;
            return new CodeViewPosition(top, ScrollX, Selection.Anchor, Selection.Caret);
        }
    }

    /// <summary>Whether a press on the text has moved off where it started, dragging a selection.</summary>
    internal bool PressMoved => Press is CodePress.OnText text && text.Moved;

    public bool HasSelection => Selection.HasSelection;
}
