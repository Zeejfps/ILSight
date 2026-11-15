using ZGF.Gui.Immediate;
using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>Everything a <see cref="CodeViewWidget.RenderCodeView"/> keeps between frames: scroll,
/// selection, folds, find. One per open tab, owned by the app. The scroll and the find's query are fields its
/// widgets take by <c>ref</c>; what keeps a rule is a part of its own.</summary>
public sealed class CodeViewState
{
    internal readonly CodeViewFrame Frame = new();

    // The bracket at the caret or just before it and the one matching it, by the document and the caret they were
    // looked up for, so they are looked up again only when either changes.
    private Memo<(ByReference<DecompiledDocument>, TextPosition), BracketPair?> _brackets;

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
            var top = rowHeight > 0f && Selection.Folds.RowCount > 0 ? CodeFolding.GetLineAt(Selection.Folds, CodeFolding.ClampRow(Selection.Folds, CodeViewWidget.GetRowAt(ScrollY, rowHeight))) : 0;
            return new CodeViewPosition
            {
                TopLine = top,
                ScrollX = ScrollX,
                Anchor = Selection.Anchor,
                Caret = Selection.Caret,
            };
        }
    }

    /// <summary>Takes in <paramref name="document"/>, unless it is the one shown already; the one shown with its notes
    /// in other words (the same <see cref="DecompiledDocument.AsMade"/>) keeps where the view stands.</summary>
    internal void ShowDocument(DecompiledDocument document)
    {
        if (ReferenceEquals(document, Layout.Document))
        {
            return;
        }

        var shown = Layout.Document;
        if (shown is not null && ReferenceEquals(shown.AsMade, document.AsMade))
        {
            TakeWords(document);
            return;
        }

        Layout.Show(document);
        Selection.Folds.Reset(document);
        Selection.Select(default, default);
        Pointer.Forget();
        ScrollX = ScrollY = 0f;
        Tooltip.Forget();
        Reveal.ForgetStale();
        Find.ClearMatch();
        Keys.Forget();
    }

    // The same lines, folds and anchors, only the notes' words changed: the scroll, the folds and the selection stay,
    // a position past a note's new end moved to it.
    private void TakeWords(DecompiledDocument document)
    {
        Layout.TakeWords(document);
        Selection.Folds.TakeWords(document);
        Selection.Select(ClampToLine(document, Selection.Anchor), ClampToLine(document, Selection.Caret));
        Tooltip.Forget();
    }

    private static TextPosition ClampToLine(DecompiledDocument document, TextPosition at)
    {
        if (at.Line >= document.Lines.Length)
        {
            return at;
        }

        return at with { Column = Math.Min(at.Column, document.Lines[at.Line].Text.Length) };
    }

    internal void KeepTop((int Line, float Within) top)
    {
        if (Layout.RowHeight > 0f)
        {
            ScrollY = CodeViewWidget.GetRowTop(CodeFolding.GetRowOf(Selection.Folds, top.Line), Layout.RowHeight) + top.Within;
        }
    }

    internal void ScrollBy(float rowHeight, float viewport, float deltaY)
    {
        if (deltaY != 0f)
        {
            ScrollY = Math.Clamp(ScrollY + deltaY, 0f, MathF.Max(0f, Selection.Folds.RowCount * rowHeight - viewport));
        }
    }

    internal void ScrollToCaret(DecompiledDocument document, float rowHeight, float advance, float textLeft, UiRect content)
    {
        var folds = Selection.Folds;
        var caret = Selection.Caret;
        var row = CodeFolding.GetRowOf(folds, caret.Line);
        var top = CodeViewWidget.GetRowTop(row, rowHeight);
        if (top < ScrollY)
        {
            ScrollY = top;
        }
        else if (top + rowHeight > ScrollY + content.Height)
        {
            ScrollY = MathF.Max(0f, top + rowHeight - content.Height);
        }

        var rowLine = CodeFolding.GetLineAt(folds, row);
        if (rowLine != caret.Line)
        {
            return;
        }

        var visible = content.Width - textLeft - advance;
        var x = Math.Min(caret.Column, document.Lines[caret.Line].Text.Length) * advance;
        if (x < ScrollX)
        {
            ScrollX = MathF.Max(0f, x - 4f * advance);
        }
        else if (x > ScrollX + visible)
        {
            ScrollX = x - visible + 4f * advance;
        }
    }

    internal BracketPair? FindBrackets(DecompiledDocument document, TextPosition caret)
    {
        if (_brackets.TryGet((new ByReference<DecompiledDocument>(document), caret), out var brackets))
        {
            return brackets;
        }

        brackets = MatchingBrackets.Match(document, caret);
        _brackets = new Memo<(ByReference<DecompiledDocument>, TextPosition), BracketPair?>((new ByReference<DecompiledDocument>(document), caret), brackets);
        return brackets;
    }
}
