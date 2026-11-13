using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>Everything a <see cref="CodeViewWidget.RenderCodeView"/> keeps between frames: scroll,
/// selection, folds, find. One per open tab, owned by the app. The scroll and the find's query are fields its
/// widgets take by <c>ref</c>; what keeps a rule is a part of its own.</summary>
public sealed class CodeViewState
{
    internal readonly CodeViewFrame Frame = new();

    /// <summary>The bracket at the caret or just before it and the one matching it, by the document and the
    /// caret they were looked up for; see <see cref="MatchingBrackets"/>.</summary>
    internal Memo<(ByReference<DecompiledDocument>, TextPosition), BracketPair?> Brackets;

    internal float ScrollX;
    internal float ScrollY;
    internal string Query = "";
    internal readonly KeptTooltip Tooltip = new();

    internal CodeFind Find { get; } = new();

    internal CaretBlink Blink { get; } = new();

    internal CodeLayout Layout { get; } = new();

    internal CodePointer Pointer { get; } = new();

    internal CodeKeys Keys { get; } = new();

    internal CodeReveal Reveal { get; } = new();

    public CodeSelection Selection { get; } = new();

    internal CodeFolds Folds => Selection.Folds;

    /// <summary>Where the view stands now; where it is to be returned to while that waits for it to be drawn.</summary>
    public CodeViewPosition Position
    {
        get
        {
            if (Reveal.Pending is PendingScroll.Restore restoring)
            {
                return restoring.Position;
            }

            var rowHeight = Layout.RowHeight;
            var top = rowHeight > 0f && Folds.RowCount > 0 ? CodeFolding.GetLineAt(Folds, CodeFolding.ClampRow(Folds, CodeViewWidget.GetRowAt(ScrollY, rowHeight))) : 0;
            return new CodeViewPosition
            {
                TopLine = top,
                ScrollX = ScrollX,
                Anchor = Selection.Anchor,
                Caret = Selection.Caret,
            };
        }
    }

    public bool HasSelection => Selection.HasSelection;
}
