using ZGF.Desktop;
using ZGF.Geometry;
using ZGF.Gui;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;
using ZGF.KeyboardModule;
using TextOverflow = ZGF.Gui.Immediate.TextOverflow;

namespace ILSight;

/// <summary>The selection in a code view, whose triple press selects the row there: a line, or a
/// collapsed fold with the lines it hides.</summary>
public sealed class CodeSelection : TextSelection<DecompiledDocument, TextPosition>
{
    /// <summary>The folds of the view this selection belongs to, which say what a block is. The selection
    /// owns them as it has to read them and is made with the view; see <see cref="CodeViewState.Folds"/>.</summary>
    internal readonly CodeFolds Folds = new();

    protected override (TextPosition Start, TextPosition End) WordAt(DecompiledDocument document, TextPosition at) => CodeText.WordAt(document, at);

    protected override (TextPosition Start, TextPosition End) BlockAt(DecompiledDocument document, TextPosition at)
    {
        var first = new TextPosition(CodeFolding.FindVisibleLine(Folds, at.Line), 0);
        var next = CodeFolding.FindNextLine(Folds, first.Line);
        if (next >= document.Lines.Length)
            return (first, CodeText.End(document));
        return (first, new TextPosition(next, 0));
    }
}

/// <summary>The press held on a code view, or last held: on a bookmark mark, a fold's chevron or a collapsed
/// fold, which a drag or click leaves alone; or on the text.</summary>
internal abstract record CodePress
{
    private CodePress()
    {
    }

    public sealed record OnControl : CodePress;

    /// <summary>A press on the text at <see cref="At"/>, with Ctrl held when <see cref="ForNewTab"/>, on the
    /// span <see cref="Span"/> of a line or none.</summary>
    public sealed record OnText(TextPosition At, bool ForNewTab, CodeSpanAt? Span) : CodePress
    {
        /// <summary>Whether the press has moved off where it started, dragging a selection.</summary>
        public bool Moved;
    }
}

/// <summary>The span <see cref="Span"/> of the line <see cref="Line"/> of a document.</summary>
internal readonly record struct CodeSpanAt(int Line, int Span);

/// <summary>Where a code view is to scroll once it is laid out: to a line revealed and flashed, back to where
/// it stood, to a find match, or to the caret.</summary>
internal abstract record PendingScroll
{
    private PendingScroll()
    {
    }

    /// <summary>Scroll <see cref="Line"/> to about a third from the top, expanding the folds that hide it, and flash it.</summary>
    public sealed record Reveal(int Line) : PendingScroll;

    public sealed record Restore(CodeViewPosition Position) : PendingScroll;

    /// <summary>Bring the columns from <see cref="ColumnStart"/> to <see cref="ColumnEnd"/> of <see cref="Line"/> into view.</summary>
    public sealed record ShowMatch(int Line, int ColumnStart, int ColumnEnd) : PendingScroll;

    /// <summary>Scroll as little as brings the caret into view, after a key moved it.</summary>
    public sealed record ShowCaret : PendingScroll;
}

/// <summary>What the pointer is over in a code view this frame: nothing; the text of a line, on one of its spans
/// or none and on a collapsed fold's chip or none; a fold's chevron; or the bookmark margin of a line. A struct
/// with a case for each, as it is made every frame.</summary>
internal readonly struct CodeHover
{
    private enum Place : byte
    {
        Nothing,
        Text,
        Chevron,
        Mark,
    }

    private readonly Place _place;
    private readonly int _line;
    private readonly int? _span;
    private readonly int? _fold;

    private CodeHover(Place place, int line, int? span, int? fold)
    {
        _place = place;
        _line = line;
        _span = span;
        _fold = fold;
    }

    public static CodeHover Nothing => default;

    /// <summary>Over the text of <paramref name="line"/>, on its span <paramref name="span"/> or none, and on
    /// the chip of the collapsed fold <paramref name="chip"/> or none.</summary>
    public static CodeHover OnText(int line, int? span, int? chip) => new(Place.Text, line, span, chip);

    public static CodeHover OnChevron(int fold) => new(Place.Chevron, 0, null, fold);

    public static CodeHover OnMark(int line) => new(Place.Mark, line, null, null);

    /// <summary>The span under the pointer, or null.</summary>
    public CodeSpanAt? Span => _place == Place.Text && _span != null ? new CodeSpanAt(_line, _span.Value) : null;

    /// <summary>The collapsed fold whose chip is under the pointer, or null.</summary>
    public int? Chip => _place == Place.Text ? _fold : null;

    /// <summary>The fold whose chevron is under the pointer, or null.</summary>
    public int? Chevron => _place == Place.Chevron ? _fold : null;

    /// <summary>The line whose bookmark margin is under the pointer, or null.</summary>
    public int? Mark => _place == Place.Mark ? _line : null;

    /// <summary>Whether a press here works a control, a chip, chevron or mark, rather than the text.</summary>
    public bool IsOnControl => Chip != null || Chevron != null || Mark != null;
}

/// <summary>The line <see cref="Line"/> flashing since the time <see cref="From"/>, after it was revealed.</summary>
internal readonly record struct CodeFlash(int Line, double From);

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
                return restoring.Position;
            var top = RowHeight > 0f && Folds.RowCount > 0 ? CodeFolding.GetLineAt(Folds, CodeFolding.ClampRow(Folds, CodeViewWidget.GetRowAt(ScrollY, RowHeight))) : 0;
            return new CodeViewPosition(top, ScrollX, Selection.Anchor, Selection.Caret);
        }
    }

    /// <summary>Whether a press on the text has moved off where it started, dragging a selection.</summary>
    internal bool PressMoved => Press is CodePress.OnText text && text.Moved;

    public bool HasSelection => Selection.HasSelection;
}

/// <summary>How the user asked a code view to change the size of its code.</summary>
public enum CodeZoom
{
    None,
    In,
    Out,
    Reset,
}

/// <summary>What the user asked a code view to do, besides changing the size of its code.</summary>
public abstract record CodeViewIntent
{
    private CodeViewIntent()
    {
    }

    /// <summary>Go to <see cref="Target"/>, in a new tab when <see cref="InNewTab"/>.</summary>
    public sealed record Navigate(CodeReference.Navigable Target, bool InNewTab) : CodeViewIntent;

    public sealed record ToggleBookmark(int Line) : CodeViewIntent;

    public sealed record Analyze(CodeReference.Entity Entity) : CodeViewIntent;

    public sealed record ShowInMetadata(CodeReference.Navigable Target) : CodeViewIntent;

    public sealed record ShowInApiReference(CodeReference.Entity Entity) : CodeViewIntent;

    public sealed record ShowInDiagram(CodeReference.Entity Type) : CodeViewIntent;

    public sealed record ShowInNewDiagram(CodeReference.Entity Type) : CodeViewIntent;
}

/// <summary>What a frame of a code view asked for: what the user asked it to do, the last thing when more
/// than one, or null; and how to change the size of its code.</summary>
public readonly record struct CodeViewResult(CodeViewIntent? Intent, CodeZoom Zoom);

public static class CodeViewWidget
{
    private const float ChevronStroke = 1.5f;

    /// <summary>Scrolls <paramref name="line"/> to about a third from the top of the view and
    /// flashes it, expanding the folds that hide it, once the view has been laid out.</summary>
    public static void RevealLine(CodeViewState state, int line)
    {
        state.Pending = new PendingScroll.Reveal(line);
    }

    /// <summary>Returns the view to <paramref name="position"/> once the next document is shown —
    /// instead of revealing a line.</summary>
    public static void RestorePosition(CodeViewState state, CodeViewPosition position)
    {
        state.Pending = new PendingScroll.Restore(position);
    }

    /// <summary>The selected text of the document last shown, lines joined by '\n', folded lines included.</summary>
    public static string GetSelectedText(CodeViewState state)
    {
        return state.Document == null ? "" : CodeText.Between(state.Document, state.Selection.Start, state.Selection.End);
    }

    /// <summary>The selected text as HTML in <paramref name="style"/>, a div per line.</summary>
    internal static string GetSelectedHtml(CodeViewState state, CodeHtmlStyle style)
    {
        return state.Document == null ? "" : CodeHtml.Between(state.Document, state.Selection.Start, state.Selection.End, style);
    }

    /// <summary>Whether fold <paramref name="fold"/> of the shown document's
    /// <see cref="DecompiledDocument.Folds"/> is collapsed.</summary>
    public static bool IsCollapsed(CodeViewState state, int fold)
    {
        return CodeFolding.IsCollapsed(state.Folds, fold);
    }

    public static void SetCollapsed(CodeViewState state, int fold, bool collapsed)
    {
        var top = FindTopAnchor(state);
        CodeFolding.SetCollapsed(state.Folds, fold, collapsed);
        KeepTop(state, top);
    }

    /// <summary>Collapses or expands the innermost fold around <paramref name="line"/>.</summary>
    public static void ToggleFold(CodeViewState state, int line)
    {
        var fold = CodeFolding.FindInnermost(state.Folds, line);
        if (fold != null)
            SetCollapsed(state, fold.Value, !CodeFolding.IsCollapsed(state.Folds, fold.Value));
    }

    public static void CollapseAll(CodeViewState state)
    {
        SetAll(state, true);
    }

    public static void ExpandAll(CodeViewState state)
    {
        SetAll(state, false);
    }

    /// <summary>Takes in <paramref name="document"/>, unless it is the one shown already.</summary>
    internal static void ShowDocument(CodeViewState state, DecompiledDocument document)
    {
        if (ReferenceEquals(document, state.Document)) return;
        state.Document = document;
        CodeFolding.Reset(state.Folds, document);
        var max = 0;
        foreach (var line in document.Lines)
            max = Math.Max(max, line.Text.Length);
        foreach (var fold in document.Folds)
            max = Math.Max(max, document.Lines[fold.StartLine].Text.Length + 1 + fold.CollapsedText.Length);
        state.MaxColumns = max;
        state.Selection.Select(default, default);
        state.Press = null;
        state.ScrollX = state.ScrollY = state.LaidOutScrollX = state.LaidOutScrollY = 0f;
        state.Marked = null;
        state.Menu = CodeMenuTarget.None;
        KeptTooltips.Forget(state.Tooltip);
        if (state.Pending is PendingScroll.ShowMatch)
            state.Pending = null;
        state.Flash = null;
        state.CurrentMatch = null;
        state.KeyScrollRows = 0;
        state.Goal = null;
    }

    internal static void RevealFolds(CodeViewState state, int line)
    {
        var top = FindTopAnchor(state);
        if (CodeFolding.Reveal(state.Folds, line))
            KeepTop(state, top);
    }

    private static void SetAll(CodeViewState state, bool collapsed)
    {
        var top = FindTopAnchor(state);
        CodeFolding.SetAll(state.Folds, collapsed);
        KeepTop(state, top);
    }

    private static (int Line, float Within) FindTopAnchor(CodeViewState state)
    {
        var folds = state.Folds;
        if (state.RowHeight <= 0f || folds.RowCount == 0) return (0, 0f);
        var row = CodeFolding.ClampRow(folds, GetRowAt(state.ScrollY, state.RowHeight));
        return (CodeFolding.GetLineAt(folds, row), state.ScrollY - GetRowTop(row, state.RowHeight));
    }

    private static void KeepTop(CodeViewState state, (int Line, float Within) top)
    {
        if (state.RowHeight > 0f)
            state.ScrollY = GetRowTop(CodeFolding.FindRowOf(state.Folds, top.Line), state.RowHeight) + top.Within;
    }

    /// <summary>The row <paramref name="offset"/> below the top of the first falls on, each <paramref name="height"/>
    /// tall; before the first or after the last when the offset is.</summary>
    internal static CodeRow GetRowAt(float offset, float height)
    {
        return new CodeRow((int)MathF.Floor(offset / height));
    }

    /// <summary>How far below the top of the first row <paramref name="row"/> starts, each <paramref name="height"/> tall.</summary>
    internal static float GetRowTop(CodeRow row, float height)
    {
        return row.Index * height;
    }

    /// <summary>Scans again when the document or query changed since <paramref name="state"/> last did; returns
    /// whether it did.</summary>
    private static bool FindMatches(CodeViewState state, DecompiledDocument document, string query)
    {
        var find = state.Find;
        if (ReferenceEquals(document, find.Document) && string.Equals(query, find.Query, StringComparison.Ordinal))
            return false;
        state.Find = new CodeFindIndex { Matches = ScanForMatches(document, query), Document = document, Query = query };
        return true;
    }

    private static List<TextPosition> ScanForMatches(DecompiledDocument document, string query)
    {
        var matches = new List<TextPosition>();
        if (query.Length == 0) return matches;

        var lines = document.Lines;
        for (var line = 0; line < lines.Length; line++)
        {
            var text = lines[line].Text;
            var at = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            while (at >= 0)
            {
                matches.Add(new TextPosition(line, at));
                at = text.IndexOf(query, at + query.Length, StringComparison.OrdinalIgnoreCase);
            }
        }
        return matches;
    }

    /// <summary>The first match at or after <paramref name="at"/>, wrapping to the first; null when there are none.</summary>
    private static int? FindFirstMatch(CodeFindIndex find, TextPosition at)
    {
        if (find.Matches.Count == 0) return null;
        var index = FindMatchOnOrAfter(find, at);
        return index < find.Matches.Count ? index : 0;
    }

    /// <summary>The index of the first match at or after <paramref name="at"/>, or the count.</summary>
    private static int FindMatchOnOrAfter(CodeFindIndex find, TextPosition at)
    {
        int low = 0, high = find.Matches.Count;
        while (low < high)
        {
            var mid = (low + high) >> 1;
            if (find.Matches[mid] < at) low = mid + 1;
            else high = mid;
        }
        return low;
    }

    private const double CaretBlinkSeconds = 0.53;

    /// <summary>Whether the caret shows this frame: only in a focused view, from when it last moved for half a
    /// blink, then off for as long, asking for the frame where that next changes.</summary>
    private static bool IsCaretShown(Ui<UiState> ui, CodeViewState state, bool focused)
    {
        if (!focused)
            return false;
        if (state.Selection.Caret != state.BlinkCaret)
        {
            state.BlinkCaret = state.Selection.Caret;
            state.BlinkFrom = ui.RealTime;
        }
        var phase = (ui.RealTime - state.BlinkFrom) % (CaretBlinkSeconds * 2);
        ui.RequestUpdateIn(CaretBlinkSeconds - phase % CaretBlinkSeconds);
        return phase < CaretBlinkSeconds;
    }

    private static readonly DisplayOptions Plain = new(HighlightMatchingBrackets: false);

    private const float FlashSeconds = 1.2f;
    private const float AutoScrollSpeed = 12f;
    private const int KeyScrollColumns = 4;
    private const float GutterPaddingLeft = 8f;
    private const float GutterPaddingRight = 8f;
    private const float CodePaddingLeft = 12f;

    /// <summary>
    /// A read-only, virtualized, syntax-colored view of <paramref name="document"/> with line
    /// numbers, folding, selection and copy, find (Ctrl+F) and reference navigation: a click on a
    /// reference asks to navigate to it, a Ctrl+click or middle click asks for a new tab. The lines in
    /// <paramref name="bookmarks"/> are marked in the gutter, dimmed when not enabled; a click in the
    /// margin before the line numbers, or Toggle bookmark in the context menu, asks to toggle one.
    /// Without <paramref name="bookmarks"/>, no line can be. Without a <paramref name="gutter"/>, there
    /// are no line numbers, fold chevrons or bookmark marks, only the text. With
    /// <paramref name="tooltips"/>, resting the pointer on a type or member shows its declaration and
    /// documentation. With <paramref name="display"/>, the code is drawn at its size with the aids it
    /// turns on, and Ctrl with the wheel, =, - or 0 asks to change the size; without, at the theme's
    /// size with line numbers only.
    /// </summary>
    public static CodeViewResult RenderCodeView(Ui<UiState> ui, string id, DecompiledDocument document, CodeViewState state,
        IReadOnlyDictionary<int, bool>? bookmarks = null, bool gutter = true, CodeTooltips? tooltips = null, DisplayOptions? display = null)
    {
        using var scope = ui.Id(id);
        ShowDocument(state, document);
        var theme = ui.Theme;
        var palette = ui.State.CodeViewPalette;
        var shown = display ?? Plain;
        var fontSize = CodeDisplay.GetFontSize(shown, theme);
        var cell = ui.MeasureCell(fontSize, family: MonoFonts.Code);
        var advance = cell.Advance;
        var rowHeight = RowHeightFor(theme, fontSize, cell);
        state.RowHeight = rowHeight;
        var folds = state.Folds;
        var digits = LineNumberDigits(document, shown);
        var markWidth = gutter ? theme.IconSize + GutterPaddingLeft : 0f;
        var foldLeft = gutter ? markWidth + digits * advance + GutterPaddingRight : 0f;
        var foldWidth = gutter && document.Folds.Length > 0 ? theme.IconSize : 0f;
        var gutterWidth = foldLeft + foldWidth;
        var textLeft = gutterWidth + CodePaddingLeft;
        var contentWidth = textLeft + state.MaxColumns * advance + CodePaddingLeft;

        var focusId = ui.GetId("focus");
        var findId = ui.GetId("find");
        var codeId = ui.GetId("code");
        var focused = ui.HasFocus(focusId);
        var findFocused = state.FindOpen && ui.HasFocus(findId);
        CodeViewIntent? intent = null;
        var zoom = CodeZoom.None;

        if (focused || findFocused)
        {
            HandleKeys(ui, state, document, focusId, focused, advance);
            if (focused && ui.IsPressed(Shortcuts.Analyze))
            {
                var analyzed = AnalyzableAt(document, state.Selection.Caret);
                if (analyzed != null)
                    intent = new CodeViewIntent.Analyze(analyzed);
            }
        }

        using (ui.Column(new Box { Width = Sizing.Grow(), Height = Sizing.Grow(), Gap = 0f, Background = theme.EditorBackground }))
        {
            if (state.FindOpen)
            {
                FindBar(ui, state, document, focusId, findId);
                ui.Separator();
            }
            ui.TabStop(focusId);

            var i = ui.Interact(codeId);
            if (display != null)
                zoom = ZoomAsked(ui, focused, i.Hovered);
            var hasRect = ui.TryGetRect(codeId, out var rect);
            ui.TryGetContentRect(codeId, out var content);

            TextPosition PositionAt(float x, float y)
            {
                var row = GetRowAt(y - rect.Y + state.LaidOutScrollY, rowHeight);
                var column = (int)MathF.Round((x - rect.X - textLeft + state.LaidOutScrollX) / advance);
                if (row.Index < 0) return CodeText.Clamp(document, new TextPosition(0, 0));
                if (row.Index >= folds.RowCount) return CodeText.End(document);
                var line = CodeFolding.GetLineAt(folds, row);
                var fold = CodeFolding.FindCollapsedAt(folds, line);
                if (fold != null && column > document.Lines[line].Text.Length)
                {
                    var end = document.Folds[fold.Value].EndLine;
                    return new TextPosition(end, document.Lines[end].Text.Length);
                }
                return CodeText.Clamp(document, new TextPosition(line, column));
            }

            var pointer = ui.Input.Pointer;
            var hover = CodeHover.Nothing;
            if (hasRect && folds.RowCount > 0 && (i.Hovered || i.Held) && pointer.X < content.Right && pointer.Y < content.Bottom)
            {
                var row = GetRowAt(pointer.Y - rect.Y + state.LaidOutScrollY, rowHeight);
                if (CodeFolding.HasRow(folds, row))
                {
                    var line = CodeFolding.GetLineAt(folds, row);
                    if (pointer.X >= rect.X + gutterWidth)
                    {
                        var column = (int)MathF.Floor((pointer.X - rect.X - textLeft + state.LaidOutScrollX) / advance);
                        if (column >= 0)
                        {
                            var span = CodeText.SpanAt(document.Lines[line].Spans, column);
                            var collapsed = CodeFolding.FindCollapsedAt(folds, line);
                            var chipStart = document.Lines[line].Text.Length + 1;
                            int? chip = null;
                            if (collapsed != null && column >= chipStart && column < chipStart + document.Folds[collapsed.Value].CollapsedText.Length)
                                chip = collapsed;
                            hover = CodeHover.OnText(line, span, chip);
                        }
                    }
                    else if (foldWidth > 0f && pointer.X >= rect.X + foldLeft)
                    {
                        var fold = Documents.FindFoldStartingAt(document, line);
                        if (fold != null)
                            hover = CodeHover.OnChevron(fold.Value);
                    }
                    else if (bookmarks != null && pointer.X < rect.X + markWidth)
                    {
                        hover = CodeHover.OnMark(line);
                    }
                }
            }
            var hoverSpan = hover.Span;
            var hovered = hoverSpan != null ? document.Lines[hoverSpan.Value.Line].Spans[hoverSpan.Value.Span] : default;
            var hoverReference = hovered.Reference;
            var hoverTarget = hoverReference is CodeReference.Navigable underPointer && !hovered.IsDefinition ? underPointer : null;
            var hoverNavigable = hoverTarget != null;

            if (i.Hovered || i.Held)
                ui.SetCursor((hoverNavigable || hover.IsOnControl) && !state.PressMoved ? MouseCursor.Hand : MouseCursor.Text);

            var scrollDeltaY = 0f;
            if (i.Pressed && hasRect)
            {
                ui.Focus(focusId, takesText: false);
                state.Press = new CodePress.OnControl();
                var mark = hover.Mark;
                var chevron = hover.Chevron;
                var chip = hover.Chip;
                if (mark != null)
                {
                    intent = new CodeViewIntent.ToggleBookmark(mark.Value);
                }
                else if (chevron != null)
                {
                    SetCollapsed(state, chevron.Value, !CodeFolding.IsCollapsed(folds, chevron.Value));
                }
                else if (chip != null)
                {
                    SetCollapsed(state, chip.Value, false);
                }
                else
                {
                    var at = PositionAt(pointer.X, pointer.Y);
                    var forNewTab = (ui.Input.Modifiers & (KeyModifiers.Control | KeyModifiers.Super)) != 0;
                    state.Press = new CodePress.OnText(at, forNewTab, hoverSpan);
                    state.Selection.Press(document, at, i.ClickCount, (ui.Input.Modifiers & KeyModifiers.Shift) != 0);
                }
            }
            else if (i.Held && hasRect && state.Press is CodePress.OnText held)
            {
                var at = PositionAt(pointer.X, pointer.Y);
                if (!held.Moved && at != held.At)
                    held.Moved = true;
                if (held.Moved)
                    state.Selection.Drag(document, at);

                var dy = pointer.Y < content.Y ? pointer.Y - content.Y : pointer.Y > content.Bottom ? pointer.Y - content.Bottom : 0f;
                var left = rect.X + gutterWidth;
                var dx = pointer.X < left ? pointer.X - left : pointer.X > content.Right ? pointer.X - content.Right : 0f;
                if (dy != 0f || dx != 0f)
                {
                    scrollDeltaY = AutoScroll(dy, rowHeight, ui.DeltaTime);
                    state.ScrollX = MathF.Max(0f, state.ScrollX + AutoScroll(dx, advance, ui.DeltaTime));
                    ui.RequestUpdate();
                }
            }

            if (i.Clicked && state.Press is CodePress.OnText clicked && !clicked.Moved && state.Selection.Clicks == 1 && (ui.Input.Modifiers & KeyModifiers.Shift) == 0)
            {
                var pressedSpan = clicked.Span;
                var reference = pressedSpan != null ? document.Lines[pressedSpan.Value.Line].Spans[pressedSpan.Value.Span] : default;
                if (reference.Reference is CodeReference.Navigable target && !reference.IsDefinition)
                    intent = new CodeViewIntent.Navigate(target, clicked.ForNewTab);
                else
                    state.Marked = reference.Reference;
            }
            if (i.ClickedWith(PointerButton.Middle) && hoverTarget != null)
                intent = new CodeViewIntent.Navigate(hoverTarget, true);
            if (i.ClickedWith(PointerButton.Right))
            {
                ui.Focus(focusId, takesText: false);
                var onDefinition = hoverSpan != null && hovered.IsDefinition;
                int? menuLine = hasRect ? PositionAt(pointer.X, pointer.Y).Line : null;
                state.Menu = new CodeMenuTarget(hoverReference, onDefinition, menuLine);
                ui.OpenContextMenu("menu");
            }

            Scroll(ui, state, document, rowHeight, advance, textLeft, content, scrollDeltaY);
            var marked = shown.HighlightUsagesAtCaret ? FindReferenceAt(document, state.Selection.Caret) : state.Marked;
            FillFrame(ui, state, document, theme, palette, cell, rowHeight, markWidth, foldLeft, foldWidth, gutterWidth, textLeft, digits,
                focused || findFocused, bookmarks, hover, hoverReference ?? marked, hoverNavigable);
            state.Frame.Caret = IsCaretShown(ui, state, focused) ? state.Selection.Caret : null;
            FillDisplayAids(state, document, shown, fontSize);

            using (ui.Box(new Box { Width = Sizing.Grow(), Height = Sizing.Grow(), Gap = 0f }))
            {
                using (var list = ui.VirtualScroll(codeId, folds.RowCount, rowHeight, ref state.ScrollX, ref state.ScrollY,
                           new Box { Background = theme.EditorBackground }))
                {
                    for (var row = list.First; row < list.End; row++)
                    {
                        using (ui.Box(new Box { Width = Sizing.Grow(min: contentWidth), Height = rowHeight }))
                            ui.Paint((state.Frame, new CodeRow(row)), PaintRow);
                    }
                }
                if (focused && ui.FocusVisible)
                {
                    using (ui.Float(0f, 0f, box: new Box { Width = Sizing.Grow(), Height = Sizing.Grow() }, z: 1))
                        ui.FocusRing(0f);
                }
            }
            if (tooltips != null && hasRect)
            {
                var under = new HoveredSpan(hoverSpan, rect.X + textLeft, rect.Y);
                var followed = ReferenceTooltip(ui, state, document, tooltips, codeId, under, advance, rowHeight);
                if (followed != null)
                    intent = followed;
            }
            state.LaidOutScrollX = state.ScrollX;
            state.LaidOutScrollY = state.ScrollY;
        }

        var picked = ContextMenu(ui, state, document, bookmarks != null);
        if (picked != null)
            intent = picked;
        return new CodeViewResult(intent, zoom);
    }

    /// <summary>How many digits wide the line numbers are, at least three; none when they are hidden.</summary>
    private static int LineNumberDigits(DecompiledDocument document, DisplayOptions display)
    {
        if (!display.ShowLineNumbers)
            return 0;
        var digits = CodeText.Digits(document.Lines.Length);
        return Math.Max(3, digits);
    }

    /// <summary>The height of a row of code at <paramref name="fontSize"/>: the theme's, grown or shrunk
    /// with the size, and never less than the font's own.</summary>
    private static float RowHeightFor(UiTheme theme, float fontSize, CellMetrics cell)
    {
        var scaled = MathF.Ceiling(theme.CodeLineHeight * fontSize / theme.MonoFontSize);
        var fontHeight = MathF.Ceiling(cell.Height);
        return MathF.Max(scaled, fontHeight);
    }

    /// <summary>The size change asked this frame: Ctrl with =, - or 0 while focused, or Ctrl with the wheel while hovered.</summary>
    private static CodeZoom ZoomAsked(Ui<UiState> ui, bool focused, bool hovered)
    {
        if (focused)
        {
            var pressed = ZoomPressed(ui);
            if (pressed != CodeZoom.None)
                return pressed;
        }
        if (!hovered)
            return CodeZoom.None;
        return ZoomScrolled(ui.Input);
    }

    private static CodeZoom ZoomPressed(Ui<UiState> ui)
    {
        if (Shortcuts.AnyPressed(ui, Shortcuts.ZoomInKeys))
            return CodeZoom.In;
        if (Shortcuts.AnyPressed(ui, Shortcuts.ZoomOutKeys))
            return CodeZoom.Out;
        if (Shortcuts.AnyPressed(ui, Shortcuts.ZoomResetKeys))
            return CodeZoom.Reset;
        return CodeZoom.None;
    }

    private static CodeZoom ZoomScrolled(UiInput input)
    {
        if (!IsShortcut(input.Modifiers))
            return CodeZoom.None;
        if (input.ScrollY > 0f)
            return CodeZoom.In;
        if (input.ScrollY < 0f)
            return CodeZoom.Out;
        return CodeZoom.None;
    }

    // The wheel zooms with Ctrl or Command held, as the framework's own zoom does.
    private static bool IsShortcut(KeyModifiers modifiers) => (modifiers & (KeyModifiers.Control | KeyModifiers.Super)) != 0;

    /// <summary>Fills what <paramref name="display"/> draws around the text: the caret's line and the
    /// brackets matching at the caret.</summary>
    private static void FillDisplayAids(CodeViewState state, DecompiledDocument document, DisplayOptions display, float fontSize)
    {
        var frame = state.Frame;
        var caret = state.Selection.Caret;
        frame.FontSize = fontSize;
        frame.CurrentLine = display.HighlightCurrentLine ? caret.Line : null;
        frame.Brackets = FindBracketsToShow(state, document, display, caret);
    }

    private static BracketPair? FindBracketsToShow(CodeViewState state, DecompiledDocument document, DisplayOptions display, TextPosition caret)
    {
        if (!display.HighlightMatchingBrackets)
            return null;
        return MatchingBrackets.Find(state, document, caret);
    }

    /// <summary>The span under the pointer and where the view's text starts on screen.</summary>
    private readonly record struct HoveredSpan(CodeSpanAt? Span, float TextX, float TextY);

    /// <summary>The tooltip of the type or member under the pointer, under its span once it has
    /// been looked up.</summary>
    private static CodeViewIntent? ReferenceTooltip(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, CodeTooltips tooltips,
        int codeId, HoveredSpan hovered, float advance, float rowHeight)
    {
        var tooltip = TooltipOf(state, document, tooltips, hovered);
        var part = 0;
        var span = hovered.Span;
        if (tooltip != null && span != null)
            part = HashCode.Combine(span.Value.Line, span.Value.Span) | 1;
        var anchor = TooltipAnchor(state, document, hovered, advance, rowHeight);
        using var popup = ui.TooltipContent(codeId, part, anchor, out var shownPart, CodeTooltipView.Look);
        KeptTooltips.Keep(state.Tooltip, tooltip, part, shownPart);
        if (!popup.IsOpen)
            return null;
        var shown = KeptTooltips.Find(state.Tooltip, shownPart);
        if (shown == null)
            return null;
        var click = CodeTooltipView.Render(ui, shown);
        return FollowLink(ui, click);
    }

    /// <summary>A link clicked in the tooltip asks to navigate to it, in a new tab with a middle
    /// click or Ctrl, and closes the tooltip.</summary>
    private static CodeViewIntent? FollowLink(Ui<UiState> ui, RichTextClick click)
    {
        if (click.Link is not CodeReference.Navigable reference)
            return null;
        ui.HideTooltip();
        var withModifier = (ui.Input.Modifiers & (KeyModifiers.Control | KeyModifiers.Super)) != 0;
        var inNewTab = click.Button == PointerButton.Middle || withModifier;
        return new CodeViewIntent.Navigate(reference, inNewTab);
    }

    /// <summary>The tooltip of the span under the pointer when it points at a type or member and
    /// its lookup is done; null otherwise, and while a press drags a selection.</summary>
    private static CodeTooltip? TooltipOf(CodeViewState state, DecompiledDocument document, CodeTooltips tooltips, HoveredSpan hovered)
    {
        var span = hovered.Span;
        if (span == null)
            return null;
        if (state.PressMoved)
            return null;
        if (document.Lines[span.Value.Line].Spans[span.Value.Span].Reference is not CodeReference.Entity entity)
            return null;
        TooltipLookups.TryGetTooltip(tooltips, entity, out var tooltip);
        return tooltip;
    }

    /// <summary>The hovered span, widened to the left by the tooltip's padding so the tooltip's
    /// text starts where the word does.</summary>
    private static UiRect TooltipAnchor(CodeViewState state, DecompiledDocument document, HoveredSpan hovered, float advance, float rowHeight)
    {
        var span = SpanRect(state, document, hovered, advance, rowHeight);
        return span with { X = span.X - CodeTooltipView.PaddingX, Width = span.Width + CodeTooltipView.PaddingX };
    }

    private static UiRect SpanRect(CodeViewState state, DecompiledDocument document, HoveredSpan hovered, float advance, float rowHeight)
    {
        var at = hovered.Span;
        if (at == null)
            return default;
        var span = document.Lines[at.Value.Line].Spans[at.Value.Span];
        var x = hovered.TextX - state.LaidOutScrollX + span.Start * advance;
        var y = hovered.TextY + GetRowTop(CodeFolding.FindRowOf(state.Folds, at.Value.Line), rowHeight) - state.LaidOutScrollY;
        return new UiRect(x, y, span.Length * advance, rowHeight);
    }

    private static float AutoScroll(float distance, float unit, float deltaTime) =>
        distance == 0f ? 0f : (distance * AutoScrollSpeed + MathF.Sign(distance) * unit * AutoScrollSpeed) * deltaTime;

    private static void FillFrame(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, UiTheme theme, CodeViewPalette palette, CellMetrics cell,
        float rowHeight, float markWidth, float foldLeft, float foldWidth, float gutterWidth, float textLeft, int digits, bool active,
        IReadOnlyDictionary<int, bool>? bookmarks, CodeHover hover, CodeReference? highlight, bool hoverNavigable)
    {
        var frame = state.Frame;
        frame.Document = document;
        frame.Folds = state.Folds.Layout;
        frame.Palette = palette;
        frame.Background = theme.EditorBackground;
        frame.GutterHover = theme.Text;
        frame.Selection = active ? theme.TextSelection : theme.SelectionInactive;
        frame.CaretColor = theme.Text;
        frame.CaretWidth = theme.CaretWidth;
        frame.ErrorText = theme.Danger;
        frame.SeparatorWidth = theme.SeparatorWidth;
        frame.OutlineWidth = theme.BorderWidth;
        frame.ChipRadius = theme.RadiusSmall;
        frame.GutterPaddingLeft = markWidth;
        frame.MarkWidth = markWidth;
        frame.IconSize = theme.IconSize;
        frame.BookmarkGlyph = ui.State.Icons.CodeBookmark;
        frame.Bookmarks = bookmarks;
        frame.HoverMark = hover.Mark;
        frame.Advance = cell.Advance;
        frame.CellHeight = cell.Height;
        frame.RowHeight = rowHeight;
        frame.FoldLeft = foldLeft;
        frame.FoldWidth = foldWidth;
        frame.GutterWidth = gutterWidth;
        frame.TextLeft = textLeft;
        frame.Digits = digits;
        frame.SelectionStart = state.Selection.Start;
        frame.SelectionEnd = state.Selection.End;
        frame.Highlight = highlight;
        frame.Underline = hoverNavigable && !state.PressMoved ? hover.Span : null;
        frame.HoverFold = hover.Chevron;
        frame.HoverChip = hover.Chip;

        var findActive = state.FindOpen && state.Query.Length > 0;
        frame.Matches = state.Find.Matches;
        frame.MatchLength = findActive ? state.Find.Length : 0;
        frame.CurrentMatch = state.CurrentMatch;

        frame.FlashLine = null;
        var flash = state.Flash;
        if (flash != null)
        {
            var t = (float)((ui.RealTime - flash.Value.From) / FlashSeconds);
            if (t >= 1f)
            {
                state.Flash = null;
            }
            else
            {
                frame.FlashLine = flash.Value.Line;
                frame.Flash = UiColor.Mix(theme.EditorBackground, palette.Flash, 1f - t);
                ui.RequestUpdate();
            }
        }
    }

    private static void Scroll(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, float rowHeight, float advance, float textLeft,
        UiRect content, float deltaY)
    {
        var folds = state.Folds;
        var viewport = content.Height;
        var pending = state.Pending;
        if (pending is PendingScroll.Restore restoring)
        {
            var restore = restoring.Position;
            if (viewport <= 0f)
            {
                ui.RequestUpdate();
            }
            else
            {
                state.Pending = null;
                var top = Math.Clamp(restore.TopLine, 0, Math.Max(0, document.Lines.Length - 1));
                RevealFolds(state, top);
                state.ScrollY = GetRowTop(CodeFolding.FindRowOf(folds, top), rowHeight);
                state.ScrollX = restore.ScrollX;
                state.Selection.Select(CodeText.Clamp(document, restore.Anchor), CodeText.Clamp(document, restore.Caret));
            }
        }

        if (viewport > 0f)
            state.PageRows = (int)MathF.Max(1f, MathF.Floor(viewport / rowHeight) - 1f);
        deltaY += state.KeyScrollRows * rowHeight;
        state.KeyScrollRows = 0;
        if (deltaY != 0f)
            state.ScrollY = Math.Clamp(state.ScrollY + deltaY, 0f, MathF.Max(0f, folds.RowCount * rowHeight - viewport));

        if (pending is PendingScroll.Reveal reveal)
        {
            if (viewport <= 0f)
            {
                ui.RequestUpdate();
                return;
            }
            var line = Math.Clamp(reveal.Line, 0, Math.Max(0, document.Lines.Length - 1));
            RevealFolds(state, line);
            state.ScrollY = MathF.Max(0f, GetRowTop(CodeFolding.FindRowOf(folds, line), rowHeight) - viewport / 3f);
            state.Flash = new CodeFlash(line, ui.RealTime);
            state.Pending = null;
            ui.RequestUpdate();
        }
        else if (pending is PendingScroll.ShowMatch match)
        {
            if (viewport <= 0f || content.Width <= 0f)
            {
                ui.RequestUpdate();
                return;
            }
            RevealFolds(state, match.Line);
            var top = GetRowTop(CodeFolding.FindRowOf(folds, match.Line), rowHeight);
            if (top < state.ScrollY || top + rowHeight > state.ScrollY + viewport)
                state.ScrollY = MathF.Max(0f, top - viewport / 3f);
            var visible = content.Width - textLeft - advance;
            var from = match.ColumnStart * advance;
            var to = match.ColumnEnd * advance;
            if (from < state.ScrollX)
                state.ScrollX = MathF.Max(0f, from - 4f * advance);
            else if (to > state.ScrollX + visible)
                state.ScrollX = to - visible + 4f * advance;
            state.Pending = null;
        }
        else if (pending is PendingScroll.ShowCaret)
        {
            if (viewport <= 0f || content.Width <= 0f)
            {
                ui.RequestUpdate();
                return;
            }
            ShowCaret(state, document, rowHeight, advance, textLeft, content);
            state.Pending = null;
        }
    }

    /// <summary>Scrolls as little as brings the caret's row, and its column on a line that is shown, into view.</summary>
    private static void ShowCaret(CodeViewState state, DecompiledDocument document, float rowHeight, float advance, float textLeft, UiRect content)
    {
        var folds = state.Folds;
        var caret = state.Selection.Caret;
        var row = CodeFolding.FindRowOf(folds, caret.Line);
        var top = GetRowTop(row, rowHeight);
        if (top < state.ScrollY)
            state.ScrollY = top;
        else if (top + rowHeight > state.ScrollY + content.Height)
            state.ScrollY = MathF.Max(0f, top + rowHeight - content.Height);
        if (CodeFolding.GetLineAt(folds, row) != caret.Line)
            return;
        var visible = content.Width - textLeft - advance;
        var x = Math.Min(caret.Column, document.Lines[caret.Line].Text.Length) * advance;
        if (x < state.ScrollX)
            state.ScrollX = MathF.Max(0f, x - 4f * advance);
        else if (x > state.ScrollX + visible)
            state.ScrollX = x - visible + 4f * advance;
    }

    /// <summary>What the span at <paramref name="at"/> refers to, or else the span just before it, so a caret
    /// at the end of a name finds it; null when neither refers to anything.</summary>
    private static CodeReference? FindReferenceAt(DecompiledDocument document, TextPosition at)
    {
        if (at.Line < 0 || at.Line >= document.Lines.Length) return null;
        var spans = document.Lines[at.Line].Spans;
        foreach (var column in (ReadOnlySpan<int>)[at.Column, at.Column - 1])
        {
            if (column < 0)
                continue;
            var index = CodeText.SpanAt(spans, column);
            if (index == null)
                continue;
            var reference = spans[index.Value].Reference;
            if (reference != null)
                return reference;
        }
        return null;
    }

    /// <summary>The type or member named at <paramref name="at"/> or just before it, as the caret
    /// sits after a word as often as in it; punctuation linked to a member does not count.</summary>
    private static CodeReference.Entity? AnalyzableAt(DecompiledDocument document, TextPosition at)
    {
        if (at.Line < 0 || at.Line >= document.Lines.Length) return null;
        var spans = document.Lines[at.Line].Spans;
        foreach (var column in (ReadOnlySpan<int>)[at.Column, at.Column - 1])
        {
            if (column < 0)
                continue;
            var index = CodeText.SpanAt(spans, column);
            if (index == null)
                continue;
            var span = spans[index.Value];
            if (span.Kind != SpanKind.Plain && span.Reference is CodeReference.Entity entity)
                return entity;
        }
        return null;
    }

    private static void HandleKeys(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, int focusId, bool focused, float advance)
    {
        var keys = ui.Input.Keys;
        for (var k = 0; k < keys.Count; k++)
        {
            if (RunShortcut(ui, state, document, focused, k))
                continue;
            if (keys[k].Key == KeyboardKey.Escape && state.FindOpen)
            {
                state.FindOpen = false;
                ui.Focus(focusId, takesText: false);
                continue;
            }
            if (focused)
                MoveCaret(state, document, keys[k].Key, keys[k].Modifiers);
        }
    }

    /// <summary>Moves the caret as <paramref name="key"/> with <paramref name="modifiers"/> asks, extending the
    /// selection with Shift, and scrolls it into view: the arrows by character, by word with Ctrl, or Option on
    /// macOS, and by row; Home and End to the ends of the line, and with Ctrl, or Command on macOS, as Command
    /// with the arrows, to the ends of the document; Page Up and Page Down by a view, scrolling with it.
    /// Ctrl with Up or Down scrolls a row and leaves the caret. Alt with Left or Right goes back and forward,
    /// outside macOS, and moves nothing here.</summary>
    private static void MoveCaret(CodeViewState state, DecompiledDocument document, KeyboardKey key, KeyModifiers modifiers)
    {
        if (document.Lines.Length == 0)
            return;
        var mac = OperatingSystem.IsMacOS();
        var command = (modifiers & (mac ? KeyModifiers.Super : KeyModifiers.Control)) != 0;
        var byWord = (modifiers & (mac ? KeyModifiers.Alt : KeyModifiers.Control)) != 0;
        var extend = (modifiers & KeyModifiers.Shift) != 0;
        var alt = (modifiers & KeyModifiers.Alt) != 0;
        var folds = state.Folds;
        var selection = state.Selection;
        var caret = CodeText.Clamp(document, selection.Caret);
        var collapses = !extend && selection.HasSelection;
        TextPosition to;
        switch (key)
        {
            case KeyboardKey.LeftArrow when !mac && alt:
            case KeyboardKey.RightArrow when !mac && alt:
                return;
            case KeyboardKey.LeftArrow when mac && command:
            case KeyboardKey.Home when !command:
                to = CodeCaret.GetLineStart(document, caret);
                break;
            case KeyboardKey.RightArrow when mac && command:
            case KeyboardKey.End when !command:
                to = CodeCaret.GetLineEnd(document, caret);
                break;
            case KeyboardKey.LeftArrow when collapses && !byWord:
                to = selection.Start;
                break;
            case KeyboardKey.RightArrow when collapses && !byWord:
                to = selection.End;
                break;
            case KeyboardKey.LeftArrow:
                to = CodeCaret.StepLeft(document, folds, caret, byWord);
                break;
            case KeyboardKey.RightArrow:
                to = CodeCaret.StepRight(document, folds, caret, byWord);
                break;
            case KeyboardKey.UpArrow when mac && command:
            case KeyboardKey.Home:
                to = default;
                break;
            case KeyboardKey.DownArrow when mac && command:
            case KeyboardKey.End:
                to = CodeCaret.GetEnd(document, folds);
                break;
            case KeyboardKey.UpArrow when !mac && command:
                state.KeyScrollRows--;
                return;
            case KeyboardKey.DownArrow when !mac && command:
                state.KeyScrollRows++;
                return;
            case KeyboardKey.UpArrow:
                MoveRows(state, document, caret, -1, extend);
                return;
            case KeyboardKey.DownArrow:
                MoveRows(state, document, caret, 1, extend);
                return;
            case KeyboardKey.PageUp:
                state.KeyScrollRows -= state.PageRows;
                MoveRows(state, document, caret, -state.PageRows, extend);
                return;
            case KeyboardKey.PageDown:
                state.KeyScrollRows += state.PageRows;
                MoveRows(state, document, caret, state.PageRows, extend);
                return;
            default:
                return;
        }
        PutCaret(state, to, extend);
    }

    /// <summary>Moves the caret <paramref name="rows"/> rows, keeping to the column it held before Up and Down
    /// began moving it.</summary>
    private static void MoveRows(CodeViewState state, DecompiledDocument document, TextPosition caret, int rows, bool extend)
    {
        var goal = state.Goal;
        var column = goal != null && goal.Value.At == state.Selection.Caret ? goal.Value.Column : caret.Column;
        var to = CodeCaret.MoveRows(document, state.Folds, caret, rows, column);
        PutCaret(state, to, extend);
        state.Goal = new CaretGoal(to, column);
    }

    private static void PutCaret(CodeViewState state, TextPosition to, bool extend)
    {
        var selection = state.Selection;
        selection.Select(extend ? selection.Anchor : to, to);
        if (state.Pending == null)
            state.Pending = new PendingScroll.ShowCaret();
    }

    /// <summary>Runs the code's shortcut the key at <paramref name="index"/> completes, if any; false
    /// for a key that is none, which then moves the view.</summary>
    private static bool RunShortcut(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, bool focused, int index)
    {
        if (ui.IsPressedAt(Shortcuts.Find, index))
        {
            if (!state.FindOpen && state.HasSelection && state.Selection.Anchor.Line == state.Selection.Caret.Line)
                state.Query = GetSelectedText(state);
            state.FindOpen = true;
            state.FocusFind = true;
            return true;
        }
        if (state.Query.Length > 0 && (ui.IsPressedAt(Shortcuts.FindNext, index) || ui.IsPressedAt(Shortcuts.FindPrevious, index)))
        {
            state.FindOpen = true;
            Step(state, document, ui.IsPressedAt(Shortcuts.FindPrevious, index) ? -1 : 1);
            return true;
        }
        if (!focused)
            return false;
        if (ui.IsPressedAt(Shortcuts.ToggleFold, index))
        {
            ToggleFold(state, state.Selection.Caret.Line);
            return true;
        }
        if (ui.IsPressedAt(Shortcuts.ToggleAllFolds, index))
        {
            if (state.Folds.AnyCollapsed) ExpandAll(state);
            else CollapseAll(state);
            return true;
        }
        if (ui.IsPressedAt(Shortcuts.CollapseFold, index))
        {
            FoldAtCaret(state, collapse: true);
            return true;
        }
        if (ui.IsPressedAt(Shortcuts.ExpandFold, index))
        {
            FoldAtCaret(state, collapse: false);
            return true;
        }
        if (ui.IsPressedAt(Shortcuts.SelectAll, index))
        {
            state.Selection.Select(default, CodeText.End(document));
            return true;
        }
        if (state.HasSelection && ui.IsPressedAt(Shortcuts.CopyPlainText, index))
        {
            ui.ClipboardText = GetSelectedText(state);
            return true;
        }
        if (state.HasSelection && ui.IsPressedAt(Shortcuts.Copy, index))
        {
            CopySelection(ui, state);
            return true;
        }
        return false;
    }

    private static void FoldAtCaret(CodeViewState state, bool collapse)
    {
        var fold = CodeFolding.FindInnermost(state.Folds, state.Selection.Caret.Line, collapsed: !collapse);
        if (fold != null)
            SetCollapsed(state, fold.Value, collapse);
    }

    /// <summary>Copies the selection as plain text and as HTML in the colors and font it is drawn in.</summary>
    private static void CopySelection(Ui<UiState> ui, CodeViewState state)
    {
        var style = CopyStyleOf(ui, state.Frame);
        ui.SetClipboardHtml(GetSelectedHtml(state, style), GetSelectedText(state));
    }

    private static CodeHtmlStyle CopyStyleOf(Ui<UiState> ui, CodeViewFrame frame)
    {
        var family = CodeDisplay.GetFontName(ui.State.Display.Font);
        return new CodeHtmlStyle(frame.Palette, frame.Background, frame.ErrorText, family, frame.FontSize, frame.RowHeight);
    }

    private static void FindBar(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, int focusId, int findId)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        using (ui.PaneToolbar())
        {
            if (state.FocusFind)
            {
                ui.Focus(findId);
                state.FocusFind = false;
            }

            var submitted = ui.TextField("find", ref state.Query, t.FindPlaceholder, Sizing.Grow(AppLayout.FindMinWidth, AppLayout.FindWidth), clearable: true);
            var queryChanged = !string.Equals(state.Query, state.Find.Query, StringComparison.Ordinal);
            if (FindMatches(state, document, state.Query))
            {
                state.CurrentMatch = queryChanged ? FindFirstMatch(state.Find, state.Selection.Start) : null;
                ShowMatch(state);
            }
            if (submitted)
                Step(state, document, (ui.Input.Modifiers & KeyModifiers.Shift) != 0 ? -1 : 1);

            var count = state.Find.Matches.Count;
            var label = state.Query.Length == 0 ? ""
                : count == 0 ? t.FindNoResults
                : ui.Memo("count", (state.CurrentMatch, count, t), static a => a.CurrentMatch != null ? a.t.FindPosition(a.CurrentMatch.Value + 1, a.count) : a.t.FindMatches(a.count));
            ui.Text(label, new Box { Width = Sizing.Grow() }, color: count == 0 && state.Query.Length > 0 ? theme.Warning : theme.TextMuted,
                size: theme.FontSizeSmall, overflow: TextOverflow.Ellipsis);
            if (ui.IconButton("previous", ui.State.Icons.CodeFindPrevious, ui.Memo("previous-tip", t, static s => s.FindPrevious("Shift+F3")), count > 0))
                Step(state, document, -1);
            if (ui.IconButton("next", ui.State.Icons.CodeFindNext, ui.Memo("next-tip", t, static s => s.FindNext("F3")), count > 0))
                Step(state, document, 1);
            if (ui.IconButton("close", ui.State.Icons.CodeFindClose, ui.Memo("close-tip", t, static s => s.FindClose("Esc"))))
            {
                state.FindOpen = false;
                ui.Focus(focusId, takesText: false);
            }
        }
    }

    private static void Step(CodeViewState state, DecompiledDocument document, int direction)
    {
        FindMatches(state, document, state.Query);
        var count = state.Find.Matches.Count;
        if (count == 0)
        {
            state.CurrentMatch = null;
            return;
        }
        var current = state.CurrentMatch;
        if (current != null)
            state.CurrentMatch = (current.Value + direction + count) % count;
        else if (direction > 0)
            state.CurrentMatch = FindFirstMatch(state.Find, state.Selection.Caret);
        else
            state.CurrentMatch = (FindMatchOnOrAfter(state.Find, state.Selection.Caret) - 1 + count) % count;
        ShowMatch(state);
    }

    private static void ShowMatch(CodeViewState state)
    {
        var current = state.CurrentMatch;
        if (current == null) return;
        var match = state.Find.Matches[current.Value];
        state.Selection.Select(match, match with { Column = match.Column + state.Find.Length });
        RevealFolds(state, match.Line);
        state.Pending = new PendingScroll.ShowMatch(match.Line, match.Column, match.Column + state.Find.Length);
    }

    /// <summary>The items that show <paramref name="entity"/> on a diagram, when it is a type.</summary>
    private static CodeViewIntent? DiagramItems(Ui<UiState> ui, CodeReference.Entity entity)
    {
        if (entity.Kind != EntityKind.Type)
            return null;
        var t = ui.State.Strings;
        CodeViewIntent? picked = null;
        if (ui.MenuItem(t.TreeShowInDiagram, id: "show-in-diagram"))
            picked = new CodeViewIntent.ShowInDiagram(entity);
        if (ui.MenuItem(t.TreeShowInNewDiagram, id: "show-in-new-diagram"))
            picked = new CodeViewIntent.ShowInNewDiagram(entity);
        return picked;
    }

    private static CodeViewIntent? ContextMenu(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, bool bookmarkable)
    {
        var t = ui.State.Strings;
        CodeViewIntent? picked = null;
        using var menu = ui.ContextMenu("menu");
        if (!menu.IsOpen) return picked;
        var target = state.Menu;

        if (ui.MenuItem(t.CodeCopy, Shortcuts.Copy, enabled: state.HasSelection, id: "copy"))
            CopySelection(ui, state);
        if (ui.MenuItem(t.CodeCopyPlainText, Shortcuts.CopyPlainText, enabled: state.HasSelection, id: "copy-plain-text"))
            ui.ClipboardText = GetSelectedText(state);
        if (ui.MenuItem(t.CodeSelectAll, Shortcuts.SelectAll, id: "select-all"))
            state.Selection.Select(default, CodeText.End(document));
        var line = target.Line;
        if (bookmarkable && line != null)
        {
            ui.MenuSeparator();
            if (ui.MenuItem(t.CodeToggleBookmark, id: "toggle-bookmark",
                    icon: new UiIcon(ui.State.Icons.CodeToggleBookmark, ui.State.CodeViewPalette.Bookmark)))
                picked = new CodeViewIntent.ToggleBookmark(line.Value);
        }
        if (document.Folds.Length > 0)
        {
            ui.MenuSeparator();
            var fold = line != null ? CodeFolding.FindInnermost(state.Folds, line.Value) : null;
            if (ui.MenuItem(t.CodeToggleFold, Shortcuts.ToggleFold, id: "toggle-fold", enabled: fold != null) && line != null)
                ToggleFold(state, line.Value);
            if (ui.MenuItem(t.CodeCollapseAll, id: "collapse-all"))
                CollapseAll(state);
            if (ui.MenuItem(t.CodeExpandAll, id: "expand-all"))
                ExpandAll(state);
        }
        var reference = target.Navigable;
        if (reference != null)
        {
            ui.MenuSeparator();
            if (ui.MenuItem(t.CodeGoToDefinition, id: "go-to-definition"))
                picked = new CodeViewIntent.Navigate(reference, false);
            if (ui.MenuItem(t.CodeOpenInNewTab, id: "open-in-new-tab"))
                picked = new CodeViewIntent.Navigate(reference, true);
        }
        var entity = target.Entity;
        if (entity != null)
        {
            if (target.Navigable == null)
                ui.MenuSeparator();
            if (ui.MenuItem(t.TreeAnalyze, Shortcuts.Analyze, id: "analyze"))
                picked = new CodeViewIntent.Analyze(entity);
            if (ui.MenuItem(t.TreeShowInApiReference, id: "show-in-api-reference", icon: new UiIcon(ui.State.Icons.LinkApiReference)))
                picked = new CodeViewIntent.ShowInApiReference(entity);
            picked = DiagramItems(ui, entity) ?? picked;
        }
        var shown = target.Metadata;
        if (shown != null)
        {
            if (target.Navigable == null && target.Entity == null)
                ui.MenuSeparator();
            if (ui.MenuItem(t.TreeShowInMetadata, id: "show-in-metadata"))
                picked = new CodeViewIntent.ShowInMetadata(shown);
        }
        return picked;
    }

    private static void PaintRow(UiPainter p, (CodeViewFrame Frame, CodeRow Row) shown)
    {
        var frame = shown.Frame;
        var row = shown.Row;
        var r = p.Rect;
        var left = p.Clip.X;
        var right = MathF.Min(r.Right, p.Clip.Right);
        var index = CodeFolding.GetLineAt(frame.Folds, row);
        var line = frame.Document.Lines[index];
        var text = line.Text;
        var fold = Documents.FindFoldStartingAt(frame.Document, index);
        var collapsedFold = fold != null && CodeFolding.IsCollapsed(frame.Folds, fold.Value) ? fold : null;
        var collapsed = collapsedFold != null;
        var chipStart = text.Length + 1;
        var chipText = collapsedFold != null ? frame.Document.Folds[collapsedFold.Value].CollapsedText : "";
        var x0 = r.X + frame.TextLeft;
        var textAreaLeft = left + frame.GutterWidth;
        var firstVisible = Math.Max(0, (int)MathF.Floor((textAreaLeft - x0) / frame.Advance));
        var endVisible = Math.Min(text.Length, (int)MathF.Ceiling((right - x0) / frame.Advance));
        var cellTop = r.Y + MathF.Floor((frame.RowHeight - frame.CellHeight) / 2);

        if (index == frame.CurrentLine)
            p.FillRect(new UiRect(textAreaLeft, r.Y, right - textAreaLeft, r.Height), frame.Palette.CurrentLine);

        if (index == frame.FlashLine)
            p.FillRect(new UiRect(textAreaLeft, r.Y, right - textAreaLeft, r.Height), frame.Flash);

        if (frame.Brackets != null)
            FillBrackets(p, frame, x0, textAreaLeft, r, index, frame.Brackets.Value);

        if (frame.Highlight != null)
        {
            foreach (var span in line.Spans)
            {
                if (span.Reference != null && span.Reference.Equals(frame.Highlight))
                    FillCells(p, frame, x0, textAreaLeft, r, span.Start, span.End, frame.Palette.Occurrence);
            }
        }

        if (frame.MatchLength > 0)
        {
            for (var m = FindFirstMatchOn(frame, index); m < frame.Matches.Count && frame.Matches[m].Line == index; m++)
            {
                if (m == frame.CurrentMatch)
                    FillCells(p, frame, x0, textAreaLeft, r, frame.Matches[m].Column, frame.Matches[m].Column + frame.MatchLength, frame.Palette.CurrentFind, frame.Palette.CurrentFindOutline, frame.OutlineWidth);
                else
                    FillCells(p, frame, x0, textAreaLeft, r, frame.Matches[m].Column, frame.Matches[m].Column + frame.MatchLength, frame.Palette.FindMatch);
            }
        }

        if (frame.SelectionStart < frame.SelectionEnd && index >= frame.SelectionStart.Line && index <= frame.SelectionEnd.Line)
        {
            var from = index == frame.SelectionStart.Line ? frame.SelectionStart.Column : 0;
            var to = index == frame.SelectionEnd.Line ? frame.SelectionEnd.Column
                : collapsedFold != null && frame.SelectionEnd.Line > frame.Document.Folds[collapsedFold.Value].EndLine ? chipStart + chipText.Length + 1
                : text.Length + 1;
            FillCells(p, frame, x0, textAreaLeft, r, from, to, frame.Selection);
        }

        if (collapsed)
        {
            var chipLeft = x0 + chipStart * frame.Advance;
            p.FillRect(new UiRect(chipLeft - frame.Advance / 2, cellTop, (chipText.Length + 1) * frame.Advance, frame.CellHeight),
                collapsedFold == frame.HoverChip ? frame.Palette.FoldChipHovered : frame.Palette.FoldChip, frame.ChipRadius);
            p.GlyphRun(new PointF(chipLeft, cellTop), GetCells(frame, chipText, 0, chipText.Length), frame.Advance, collapsedFold == frame.HoverChip ? frame.GutterHover : frame.Palette.GutterText,
                frame.FontSize, family: MonoFonts.Code);
        }

        if (endVisible > firstVisible)
        {
            var spans = line.Spans;
            var error = frame.Document.Error != null;
            var first = CodeText.SpanAt(spans, firstVisible) ?? 0;
            var underlined = frame.Underline != null && frame.Underline.Value.Line == index ? frame.Underline.Value.Span : (int?)null;
            for (var s = first; s < spans.Length && spans[s].Start < endVisible; s++)
            {
                var span = spans[s];
                var start = Math.Max(span.Start, firstVisible);
                var end = Math.Min(span.End, endVisible);
                if (end <= start) continue;
                var cells = GetCells(frame, text, start, end);
                p.GlyphRun(new PointF(x0 + start * frame.Advance, cellTop), cells, frame.Advance, error ? frame.ErrorText : frame.Palette[span.Kind],
                    frame.FontSize, family: MonoFonts.Code, underline: s == underlined);
            }
        }

        var caret = frame.Caret;
        if (caret != null)
            PaintCaret(p, frame, caret.Value, x0, r, index, collapsedFold, chipStart + chipText.Length);

        if (frame.GutterWidth <= 0f) return;
        p.FillRect(new UiRect(left, r.Y, frame.GutterWidth, r.Height), frame.Background);
        p.FillRect(new UiRect(left + frame.GutterWidth - frame.SeparatorWidth, r.Y, frame.SeparatorWidth, r.Height), frame.Palette.GutterSeparator);
        if (frame.Digits > 0)
            PaintLineNumber(p, frame, left, cellTop, index);
        if (frame.Bookmarks != null && (frame.Bookmarks.TryGetValue(index, out var enabled) || index == frame.HoverMark))
        {
            var color = !frame.Bookmarks.ContainsKey(index) ? frame.Palette.GutterText : enabled ? frame.Palette.Bookmark : frame.Palette.GutterText;
            var size = MathF.Min(frame.IconSize, frame.RowHeight);
            p.Glyph(new UiRect(left + (frame.MarkWidth - size) / 2, r.Y + (r.Height - size) / 2, size, size), frame.BookmarkGlyph, color, size);
        }
        if (fold != null && frame.FoldWidth > 0f)
            PaintChevron(p, frame, left + frame.FoldLeft + frame.FoldWidth / 2, r.Y + r.Height / 2, collapsed, fold == frame.HoverFold ? frame.GutterHover : frame.Palette.GutterText);
    }

    /// <summary><paramref name="caret"/>, on the line <paramref name="index"/> when it stands there, or after the chip of the
    /// collapsed fold <paramref name="collapsedFold"/>, ending at column <paramref name="chipEnd"/>, when that
    /// hides it.</summary>
    private static void PaintCaret(UiPainter p, CodeViewFrame frame, TextPosition caret, float x0, UiRect row, int index, int? collapsedFold, int chipEnd)
    {
        int column;
        if (caret.Line == index)
            column = caret.Column;
        else if (collapsedFold != null && caret.Line > index && caret.Line <= frame.Document.Folds[collapsedFold.Value].EndLine)
            column = chipEnd;
        else
            return;
        p.FillRect(new UiRect(x0 + column * frame.Advance, row.Y, frame.CaretWidth, row.Height), frame.CaretColor);
    }

    /// <summary>The line's number, right-aligned, brighter on the highlighted current line.</summary>
    private static void PaintLineNumber(UiPainter p, CodeViewFrame frame, float left, float cellTop, int index)
    {
        var number = GetNumberCells(frame, index + 1);
        var color = index == frame.CurrentLine ? frame.GutterHover : frame.Palette.GutterText;
        var x = left + frame.GutterPaddingLeft + (frame.Digits - number.Length) * frame.Advance;
        p.GlyphRun(new PointF(x, cellTop), number, frame.Advance, color, frame.FontSize, family: MonoFonts.Code);
    }

    private static void FillBrackets(UiPainter p, CodeViewFrame frame, float x0, float left, UiRect row, int index, BracketPair brackets)
    {
        if (brackets.Open.Line == index)
            FillCells(p, frame, x0, left, row, brackets.Open.Column, brackets.Open.Column + 1, frame.Palette.MatchingBracket);
        if (brackets.Close.Line == index)
            FillCells(p, frame, x0, left, row, brackets.Close.Column, brackets.Close.Column + 1, frame.Palette.MatchingBracket);
    }

    private static void PaintChevron(UiPainter p, CodeViewFrame frame, float cx, float cy, bool collapsed, uint color)
    {
        var half = MathF.Max(2f, MathF.Floor(frame.Advance * 0.4f));
        p.DrawDisclosure(new PointF(cx, cy), half, !collapsed, color, ChevronStroke);
    }

    private static void FillCells(UiPainter p, CodeViewFrame frame, float x0, float left, UiRect row, int from, int to, uint color, uint outline = 0, float outlineWidth = 0f)
    {
        var x = MathF.Max(left, x0 + from * frame.Advance);
        var right = x0 + to * frame.Advance;
        if (right > x)
            p.FillRect(new UiRect(x, row.Y, right - x, row.Height), color, 0f, outline, outlineWidth);
    }

    private static int FindFirstMatchOn(CodeViewFrame frame, int line)
    {
        int low = 0, high = frame.Matches.Count;
        while (low < high)
        {
            var mid = (low + high) >> 1;
            if (frame.Matches[mid].Line < line) low = mid + 1;
            else high = mid;
        }
        return low;
    }

    private static ReadOnlySpan<int> GetCells(CodeViewFrame frame, string text, int start, int end)
    {
        var count = end - start;
        if (frame.CodePoints.Length < count)
            frame.CodePoints = new int[Math.Max(count, frame.CodePoints.Length * 2)];
        for (var i = 0; i < count; i++)
        {
            var at = start + i;
            var c = text[at];
            if (char.IsHighSurrogate(c) && at + 1 < text.Length && char.IsLowSurrogate(text[at + 1]))
            {
                frame.CodePoints[i] = char.ConvertToUtf32(c, text[at + 1]);
                if (i + 1 < count) frame.CodePoints[++i] = ' ';
            }
            else
            {
                frame.CodePoints[i] = char.IsSurrogate(c) ? ' ' : c;
            }
        }
        return frame.CodePoints.AsSpan(0, count);
    }

    private static ReadOnlySpan<int> GetNumberCells(CodeViewFrame frame, int value)
    {
        var digits = CodeText.Digits(value);
        var cells = frame.CodePoints.AsSpan(0, digits);
        for (var i = digits - 1; i >= 0; i--)
        {
            cells[i] = '0' + value % 10;
            value /= 10;
        }
        return cells;
    }
}
