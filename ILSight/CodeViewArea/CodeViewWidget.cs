using ZGF.Desktop;
using ZGF.Geometry;
using ZGF.Gui;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ZGF.KeyboardModule;
using TextOverflow = ZGF.Gui.Immediate.TextOverflow;
using System.Text;
using ILSight.BookmarkArea;
using ILSight.CodeTooltipArea;

namespace ILSight.CodeViewArea;

public static class CodeViewWidget
{
    private const float ChevronStroke = 1.5f;

    /// <summary>Scrolls <paramref name="line"/> to about a third from the top of the view and
    /// flashes it, expanding the folds that hide it, once the view has been laid out.</summary>
    public static void RevealLine(CodeViewState state, int line)
    {
        state.Reveal.Ask(new PendingScroll.Reveal(line));
    }

    /// <summary>Returns the view to <paramref name="position"/> once the next document is shown —
    /// instead of revealing a line.</summary>
    public static void RestorePosition(CodeViewState state, CodeViewPosition position)
    {
        state.Reveal.Ask(new PendingScroll.Restore(position));
    }

    /// <summary>The selected text of the document last shown, lines joined by '\n', folded lines included.</summary>
    public static string GetSelectedText(CodeViewState state)
    {
        return state.Layout.Document is null ? "" : CodeText.Between(state.Layout.Document, state.Selection.Start, state.Selection.End);
    }

    private static string GetSelectedHtml(CodeViewState state, CodeHtmlStyle style)
    {
        return state.Layout.Document is null ? "" : CodeHtml.Between(state.Layout.Document, state.Selection.Start, state.Selection.End, style);
    }

    public static void SetCollapsed(CodeViewState state, int fold, bool collapsed)
    {
        var top = GetTopAnchor(state);
        CodeFolding.SetCollapsed(state.Folds, fold, collapsed);
        KeepTop(state, top);
    }

    /// <summary>Collapses or expands the innermost fold around <paramref name="line"/>.</summary>
    public static void ToggleFold(CodeViewState state, int line)
    {
        var fold = CodeFolding.FindInnermost(state.Folds, line);
        if (fold is not null)
        {
            SetCollapsed(state, fold.Value, !CodeFolding.IsCollapsed(state.Folds, fold.Value));
        }
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
        if (ReferenceEquals(document, state.Layout.Document))
        {
            return;
        }

        state.Layout.Show(document);
        CodeFolding.Reset(state.Folds, document);
        state.Selection.Select(default, default);
        state.Pointer.Forget();
        state.ScrollX = state.ScrollY = 0f;
        state.Tooltip.Forget();
        state.Reveal.ForgetStale();
        state.Find.ClearMatch();
        state.Keys.Forget();
    }

    internal static void RevealFolds(CodeViewState state, int line)
    {
        var top = GetTopAnchor(state);
        if (CodeFolding.TryReveal(state.Folds, line))
        {
            KeepTop(state, top);
        }
    }

    private static void SetAll(CodeViewState state, bool collapsed)
    {
        var top = GetTopAnchor(state);
        CodeFolding.SetAll(state.Folds, collapsed);
        KeepTop(state, top);
    }

    private static (int Line, float Within) GetTopAnchor(CodeViewState state)
    {
        var folds = state.Folds;
        if (state.Layout.RowHeight <= 0f || folds.RowCount == 0)
        {
            return (0, 0f);
        }

        var row = CodeFolding.ClampRow(folds, GetRowAt(state.ScrollY, state.Layout.RowHeight));
        return (CodeFolding.GetLineAt(folds, row), state.ScrollY - GetRowTop(row, state.Layout.RowHeight));
    }

    private static void KeepTop(CodeViewState state, (int Line, float Within) top)
    {
        if (state.Layout.RowHeight > 0f)
        {
            state.ScrollY = GetRowTop(CodeFolding.GetRowOf(state.Folds, top.Line), state.Layout.RowHeight) + top.Within;
        }
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

    private const double CaretBlinkSeconds = 0.53;

    private static bool IsCaretShown(Ui<UiState> ui, CodeViewState state, bool focused)
    {
        if (!focused)
        {
            return false;
        }

        state.Blink.Follow(state.Selection.Caret, ui.RealTime);
        var phase = (ui.RealTime - state.Blink.From) % (CaretBlinkSeconds * 2);
        // Nothing else may draw a frame when the caret next shows or hides.
        ui.RequestUpdateIn(CaretBlinkSeconds - phase % CaretBlinkSeconds);
        return phase < CaretBlinkSeconds;
    }

    private static readonly DisplayOptions Plain = new DisplayOptions { HighlightMatchingBrackets = false };

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
    /// size with line numbers only. With <paramref name="closedAssembly"/>, the name of the closed assembly the code is
    /// of (<see cref="TabLoad.ShowClosed"/>), nothing that would read it is asked for: the menu shows following,
    /// analyzing and showing a reference disabled, and a hovered type or member's tooltip, each saying it was closed.
    /// </summary>
    public static CodeViewResult RenderCodeView(Ui<UiState> ui, string id, DecompiledDocument document, CodeViewState state,
        IReadOnlyDictionary<int, bool>? bookmarks = null, bool gutter = true, CodeTooltips? tooltips = null, DisplayOptions? display = null,
        string? closedAssembly = null)
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
        state.Layout.TakeRowHeight(rowHeight);
        var folds = state.Folds;
        var digits = LineNumberDigits(document, shown);
        var markWidth = gutter ? theme.IconSize + GutterPaddingLeft : 0f;
        var foldLeft = gutter ? markWidth + digits * advance + GutterPaddingRight : 0f;
        var foldWidth = gutter && document.Folds.Length > 0 ? theme.IconSize : 0f;
        var gutterWidth = foldLeft + foldWidth;
        var textLeft = gutterWidth + CodePaddingLeft;
        var contentWidth = textLeft + state.Layout.MaxColumns * advance + CodePaddingLeft;

        var focusId = ui.GetId("focus");
        var findId = ui.GetId("find");
        var codeId = ui.GetId("code");
        var focused = ui.HasFocus(focusId);
        var findFocused = state.Find.IsOpen && ui.HasFocus(findId);
        CodeViewIntent? intent = null;
        var zoom = CodeZoom.None;

        if (focused || findFocused)
        {
            HandleKeys(ui, state, document, focusId, focused, advance);
            if (focused && ui.IsPressed(Shortcuts.Analyze))
            {
                var analyzed = AnalyzableAt(document, state.Selection.Caret);
                if (analyzed != null)
                {
                    intent = new CodeViewIntent.Analyze(analyzed);
                }
            }
        }

        using (ui.Column(new Box { Width = Sizing.Grow(), Height = Sizing.Grow(), Gap = 0f, Background = theme.EditorBackground }))
        {
            if (state.Find.IsOpen)
            {
                FindBar(ui, state, document, focusId, findId);
                ui.Separator();
            }
            ui.TabStop(focusId);

            var i = ui.Interact(codeId);
            if (display is not null)
            {
                zoom = ZoomAsked(ui, focused, i.Hovered);
            }

            var hasRect = ui.TryGetRect(codeId, out var rect);
            ui.TryGetContentRect(codeId, out var content);

            TextPosition PositionAt(float x, float y)
            {
                var row = GetRowAt(y - rect.Y + state.Layout.ScrollY, rowHeight);
                var column = (int)MathF.Round((x - rect.X - textLeft + state.Layout.ScrollX) / advance);
                if (row.Index < 0)
                {
                    return CodeText.Clamp(document, new TextPosition { Line = 0, Column = 0 });
                }

                if (row.Index >= folds.RowCount)
                {
                    return CodeText.End(document);
                }

                var line = CodeFolding.GetLineAt(folds, row);
                var fold = CodeFolding.FindCollapsedAt(folds, line);
                if (fold is not null && column > document.Lines[line].Text.Length)
                {
                    var end = document.Folds[fold.Value].EndLine;
                    return new TextPosition { Line = end, Column = document.Lines[end].Text.Length };
                }
                return CodeText.Clamp(document, new TextPosition { Line = line, Column = column });
            }

            var pointer = ui.Input.Pointer;
            var hover = CodeHover.Nothing;
            if (hasRect && folds.RowCount > 0 && (i.Hovered || i.Held) && pointer.X < content.Right && pointer.Y < content.Bottom)
            {
                var row = GetRowAt(pointer.Y - rect.Y + state.Layout.ScrollY, rowHeight);
                if (CodeFolding.HasRow(folds, row))
                {
                    var line = CodeFolding.GetLineAt(folds, row);
                    if (pointer.X >= rect.X + gutterWidth)
                    {
                        var column = (int)MathF.Floor((pointer.X - rect.X - textLeft + state.Layout.ScrollX) / advance);
                        if (column >= 0)
                        {
                            var span = CodeText.SpanAt(document.Lines[line].Spans, column);
                            var collapsed = CodeFolding.FindCollapsedAt(folds, line);
                            var chipStart = document.Lines[line].Text.Length + 1;
                            int? chip = null;
                            if (collapsed is not null && column >= chipStart && column < chipStart + document.Folds[collapsed.Value].CollapsedText.Length)
                            {
                                chip = collapsed;
                            }

                            hover = CodeHover.OnText(line, span, chip);
                        }
                    }
                    else if (foldWidth > 0f && pointer.X >= rect.X + foldLeft)
                    {
                        var fold = Documents.FindFoldStartingAt(document, line);
                        if (fold is not null)
                        {
                            hover = CodeHover.OnChevron(fold.Value);
                        }
                    }
                    else if (bookmarks is not null && pointer.X < rect.X + markWidth)
                    {
                        hover = CodeHover.OnMark(line);
                    }
                }
            }
            var hoverSpan = hover.Span;
            var hovered = hoverSpan is not null ? document.Lines[hoverSpan.Value.Line].Spans[hoverSpan.Value.Span] : default;
            var hoverReference = hovered.Reference;
            var hoverTarget = hoverReference is CodeReference.Navigable underPointer && !hovered.IsDefinition ? underPointer : null;
            var hoverNavigable = hoverTarget != null;

            if (i.Hovered || i.Held)
            {
                ui.SetCursor((hoverNavigable || hover.IsOnControl) && !state.Pointer.PressMoved ? MouseCursor.Hand : MouseCursor.Text);
            }

            var scrollDeltaY = 0f;
            if (i.Pressed && hasRect)
            {
                ui.Focus(focusId, takesText: false);
                state.Pointer.PressOn(new CodePress.OnControl());
                var mark = hover.Mark;
                var chevron = hover.Chevron;
                var chip = hover.Chip;
                if (mark is not null)
                {
                    intent = new CodeViewIntent.ToggleBookmark(mark.Value);
                }
                else if (chevron is not null)
                {
                    SetCollapsed(state, chevron.Value, !CodeFolding.IsCollapsed(folds, chevron.Value));
                }
                else if (chip is not null)
                {
                    SetCollapsed(state, chip.Value, false);
                }
                else
                {
                    var at = PositionAt(pointer.X, pointer.Y);
                    var forNewTab = (ui.Input.Modifiers & (KeyModifiers.Control | KeyModifiers.Super)) != 0;
                    state.Pointer.PressOn(new CodePress.OnText { At = at, ForNewTab = forNewTab, Span = hoverSpan });
                    state.Selection.Press(document, at, i.ClickCount, (ui.Input.Modifiers & KeyModifiers.Shift) != 0);
                }
            }
            else if (i.Held && hasRect && state.Pointer.Press is CodePress.OnText held)
            {
                var at = PositionAt(pointer.X, pointer.Y);
                if (!held.Moved && at != held.At)
                {
                    held.Moved = true;
                }

                if (held.Moved)
                {
                    state.Selection.Drag(document, at);
                }

                var dy = pointer.Y < content.Y ? pointer.Y - content.Y : pointer.Y > content.Bottom ? pointer.Y - content.Bottom : 0f;
                var left = rect.X + gutterWidth;
                var dx = pointer.X < left ? pointer.X - left : pointer.X > content.Right ? pointer.X - content.Right : 0f;
                if (dy != 0f || dx != 0f)
                {
                    scrollDeltaY = AutoScroll(dy, rowHeight, ui.DeltaTime);
                    state.ScrollX = MathF.Max(0f, state.ScrollX + AutoScroll(dx, advance, ui.DeltaTime));
                    ui.RequestAnimationFrame();
                }
            }

            if (i.Clicked && state.Pointer.Press is CodePress.OnText clicked && !clicked.Moved && state.Selection.Clicks == 1 && (ui.Input.Modifiers & KeyModifiers.Shift) == 0)
            {
                var pressedSpan = clicked.Span;
                var reference = pressedSpan is not null ? document.Lines[pressedSpan.Value.Line].Spans[pressedSpan.Value.Span] : default;
                if (reference.Reference is CodeReference.Navigable target && !reference.IsDefinition)
                {
                    intent = new CodeViewIntent.Navigate { Target = target, InNewTab = clicked.ForNewTab };
                }
                else
                {
                    state.Pointer.Mark(reference.Reference);
                }
            }
            if (i.ClickedWith(PointerButton.Middle) && hoverTarget != null)
            {
                intent = new CodeViewIntent.Navigate { Target = hoverTarget, InNewTab = true };
            }

            if (i.ClickedWith(PointerButton.Right))
            {
                ui.Focus(focusId, takesText: false);
                var onDefinition = hoverSpan is not null && hovered.IsDefinition;
                int? menuLine = hasRect ? PositionAt(pointer.X, pointer.Y).Line : null;
                state.Pointer.OpenMenuOn(new CodeMenuTarget
                {
                    Reference = hoverReference,
                    OnDefinition = onDefinition,
                    Line = menuLine,
                    Analyzable = AnalyzableIn(hovered),
                });
                ui.OpenContextMenu("menu");
            }

            Scroll(ui, state, document, rowHeight, advance, textLeft, content, scrollDeltaY);
            var marked = shown.HighlightUsagesAtCaret ? FindReferenceAt(document, state.Selection.Caret) : state.Pointer.Marked;
            var codeGutter = new CodeGutter
            {
                MarkWidth = markWidth,
                Digits = digits,
                FoldLeft = foldLeft,
                FoldWidth = foldWidth,
                Width = gutterWidth,
                TextLeft = textLeft,
            };
            FillFrame(ui, state, document, theme, palette, cell, rowHeight, codeGutter, focused || findFocused, bookmarks, hover, hoverReference ?? marked,
                hoverNavigable);
            state.Frame.ShowCaret(IsCaretShown(ui, state, focused) ? state.Selection.Caret : null);
            FillDisplayAids(state, document, shown, fontSize);

            using (ui.Box(new Box { Width = Sizing.Grow(), Height = Sizing.Grow(), Gap = 0f }))
            {
                using (var list = ui.VirtualScroll(codeId, folds.RowCount, rowHeight, ref state.ScrollX, ref state.ScrollY,
                           new Box { Background = theme.EditorBackground }))
                {
                    for (var row = list.First; row < list.End; row++)
                    {
                        using (ui.Box(new Box { Width = Sizing.Grow(min: contentWidth), Height = rowHeight }))
                        {
                            ui.Paint((state.Frame, new CodeRow(row)), PaintRow);
                        }
                    }
                }
                if (focused && ui.FocusVisible)
                {
                    using (ui.Float(0f, 0f, box: new Box { Width = Sizing.Grow(), Height = Sizing.Grow() }, z: 1))
                    {
                        ui.FocusRing(0f);
                    }
                }
            }
            if (tooltips is not null && hasRect)
            {
                var under = new HoveredSpan { Span = hoverSpan, TextX = rect.X + textLeft, TextY = rect.Y };
                if (closedAssembly is not null)
                {
                    ClosedTooltip(ui, state, document, codeId, under, advance, rowHeight, closedAssembly);
                }
                else
                {
                    var followed = ReferenceTooltip(ui, state, document, tooltips, codeId, under, advance, rowHeight);
                    if (followed is not null)
                    {
                        intent = followed;
                    }
                }
            }
            state.Layout.KeepScroll(state.ScrollX, state.ScrollY);
        }

        var picked = ContextMenu(ui, state, document, bookmarks is not null, closedAssembly);
        if (picked is not null)
        {
            intent = picked;
        }

        return new CodeViewResult { Intent = intent, Zoom = zoom };
    }

    private static int LineNumberDigits(DecompiledDocument document, DisplayOptions display)
    {
        if (!display.ShowLineNumbers)
        {
            return 0;
        }

        var digits = CodeText.Digits(document.Lines.Length);
        return Math.Max(3, digits);
    }

    private static float RowHeightFor(UiTheme theme, float fontSize, CellMetrics cell)
    {
        var scaled = MathF.Ceiling(theme.CodeLineHeight * fontSize / theme.MonoFontSize);
        var fontHeight = MathF.Ceiling(cell.Height);
        return MathF.Max(scaled, fontHeight);
    }

    private static CodeZoom ZoomAsked(Ui<UiState> ui, bool focused, bool hovered)
    {
        if (focused)
        {
            var pressed = ZoomPressed(ui);
            if (pressed != CodeZoom.None)
            {
                return pressed;
            }
        }
        if (!hovered)
        {
            return CodeZoom.None;
        }

        return ZoomScrolled(ui.Input);
    }

    private static CodeZoom ZoomPressed(Ui<UiState> ui)
    {
        if (Shortcuts.AnyPressed(ui, Shortcuts.ZoomInKeys))
        {
            return CodeZoom.In;
        }

        if (Shortcuts.AnyPressed(ui, Shortcuts.ZoomOutKeys))
        {
            return CodeZoom.Out;
        }

        if (Shortcuts.AnyPressed(ui, Shortcuts.ZoomResetKeys))
        {
            return CodeZoom.Reset;
        }

        return CodeZoom.None;
    }

    private static CodeZoom ZoomScrolled(UiInput input)
    {
        if (!IsShortcut(input.Modifiers))
        {
            return CodeZoom.None;
        }

        if (input.ScrollY > 0f)
        {
            return CodeZoom.In;
        }

        if (input.ScrollY < 0f)
        {
            return CodeZoom.Out;
        }

        return CodeZoom.None;
    }

    // The wheel zooms with Ctrl or Command held, as the framework's own zoom does.
    private static bool IsShortcut(KeyModifiers modifiers)
    {
        return (modifiers & (KeyModifiers.Control | KeyModifiers.Super)) != 0;
    }

    private static void FillDisplayAids(CodeViewState state, DecompiledDocument document, DisplayOptions display, float fontSize)
    {
        var caret = state.Selection.Caret;
        int? currentLine = display.HighlightCurrentLine ? caret.Line : null;
        state.Frame.ShowAids(fontSize, currentLine, FindBracketsToShow(state, document, display, caret));
    }

    private static BracketPair? FindBracketsToShow(CodeViewState state, DecompiledDocument document, DisplayOptions display, TextPosition caret)
    {
        if (!display.HighlightMatchingBrackets)
        {
            return null;
        }

        return MatchingBrackets.Find(state, document, caret);
    }

    private readonly record struct HoveredSpan
    {
        public required CodeSpanAt? Span { get; init; }

        public required float TextX { get; init; }

        public required float TextY { get; init; }
    }

    private static CodeViewIntent? ReferenceTooltip(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, CodeTooltips tooltips,
        int codeId, HoveredSpan hovered, float advance, float rowHeight)
    {
        var tooltip = TooltipOf(state, document, tooltips, hovered);
        var part = 0;
        var span = hovered.Span;
        if (tooltip != null && span is not null)
        {
            part = HashCode.Combine(span.Value.Line, span.Value.Span) | 1;
        }

        var anchor = TooltipAnchor(state, document, hovered, advance, rowHeight);
        using var popup = ui.TooltipContent(codeId, part, anchor, out var shownPart, CodeTooltipView.Look);
        state.Tooltip.Keep(tooltip, part, shownPart);
        if (!popup.IsOpen)
        {
            return null;
        }

        var shown = state.Tooltip.Find(shownPart);
        if (shown == null)
        {
            return null;
        }

        var click = CodeTooltipView.Render(ui, shown);
        return FollowLink(ui, click);
    }

    private static void ClosedTooltip(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, int codeId, HoveredSpan hovered,
        float advance, float rowHeight, string closedAssembly)
    {
        var part = 0;
        var entity = FindHoveredEntity(state, document, hovered);
        var span = hovered.Span;
        if (entity is not null && span is not null)
        {
            part = HashCode.Combine(span.Value.Line, span.Value.Span) | 1;
        }

        var anchor = TooltipAnchor(state, document, hovered, advance, rowHeight);
        using var popup = ui.TooltipContent(codeId, part, anchor, out _, CodeTooltipView.Look);
        if (!popup.IsOpen)
        {
            return;
        }

        CodeTooltipView.RenderClosed(ui, closedAssembly);
    }

    private static CodeViewIntent? FollowLink(Ui<UiState> ui, RichTextClick click)
    {
        if (click.Link is not CodeReference.Navigable reference)
        {
            return null;
        }

        ui.HideTooltip();
        var withModifier = (ui.Input.Modifiers & (KeyModifiers.Control | KeyModifiers.Super)) != 0;
        var inNewTab = click.Button == PointerButton.Middle || withModifier;
        return new CodeViewIntent.Navigate { Target = reference, InNewTab = inNewTab };
    }

    private static Lookup<CodeTooltip>? TooltipOf(CodeViewState state, DecompiledDocument document, CodeTooltips tooltips, HoveredSpan hovered)
    {
        var entity = FindHoveredEntity(state, document, hovered);
        if (entity is null)
        {
            return null;
        }

        return tooltips.FindShown(entity);
    }

    private static CodeReference.Entity? FindHoveredEntity(CodeViewState state, DecompiledDocument document, HoveredSpan hovered)
    {
        var span = hovered.Span;
        if (span is null)
        {
            return null;
        }

        if (state.Pointer.PressMoved)
        {
            return null;
        }

        return document.Lines[span.Value.Line].Spans[span.Value.Span].Reference as CodeReference.Entity;
    }

    // Widened to the left by the tooltip's padding so that the tooltip's text starts where the word does.
    private static UiRect TooltipAnchor(CodeViewState state, DecompiledDocument document, HoveredSpan hovered, float advance, float rowHeight)
    {
        var span = SpanRect(state, document, hovered, advance, rowHeight);
        return span with { X = span.X - CodeTooltipView.PaddingX, Width = span.Width + CodeTooltipView.PaddingX };
    }

    private static UiRect SpanRect(CodeViewState state, DecompiledDocument document, HoveredSpan hovered, float advance, float rowHeight)
    {
        var at = hovered.Span;
        if (at is null)
        {
            return default;
        }

        var span = document.Lines[at.Value.Line].Spans[at.Value.Span];
        var x = hovered.TextX - state.Layout.ScrollX + span.Start * advance;
        var y = hovered.TextY + GetRowTop(CodeFolding.GetRowOf(state.Folds, at.Value.Line), rowHeight) - state.Layout.ScrollY;
        return new UiRect(x, y, span.Length * advance, rowHeight);
    }

    private static float AutoScroll(float distance, float unit, float deltaTime)
    {
        return distance == 0f ? 0f : (distance * AutoScrollSpeed + MathF.Sign(distance) * unit * AutoScrollSpeed) * deltaTime;
    }

    private static void FillFrame(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, UiTheme theme, CodeViewPalette palette, CellMetrics cell,
        float rowHeight, CodeGutter gutter, bool active, IReadOnlyDictionary<int, bool>? bookmarks, CodeHover hover, CodeReference? highlight,
        bool hoverNavigable)
    {
        var frame = state.Frame;
        frame.Lay(document, state.Folds.Layout, palette, theme, cell, rowHeight, gutter, ui.State.Icons.CodeBookmark, active);
        var underline = hoverNavigable && !state.Pointer.PressMoved ? hover.Span : null;
        frame.Mark(state.Selection.Start, state.Selection.End, highlight, underline, hover, bookmarks);
        var findActive = state.Find.IsOpen && state.Query.Length > 0;
        frame.ShowMatches(state.Find.Index.Matches, findActive ? state.Find.Index.Length : 0, state.Find.CurrentMatch);
        ShowFlash(ui, state, theme, palette);
    }

    // The flash fades over FlashSeconds, and is let go once it has faded.
    private static void ShowFlash(Ui<UiState> ui, CodeViewState state, UiTheme theme, CodeViewPalette palette)
    {
        var flash = state.Reveal.Flash;
        if (flash is null)
        {
            state.Frame.ShowFlash(null, 0u);
            return;
        }

        var t = (float)((ui.RealTime - flash.Value.From) / FlashSeconds);
        if (t >= 1f)
        {
            state.Reveal.EndFlash();
            state.Frame.ShowFlash(null, 0u);
            return;
        }

        state.Frame.ShowFlash(flash.Value.Line, UiColor.Mix(theme.EditorBackground, palette.Flash, 1f - t));
        ui.RequestAnimationFrame();
    }

    private static void Scroll(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, float rowHeight, float advance, float textLeft,
        UiRect content, float deltaY)
    {
        var viewport = content.Height;
        state.Layout.TakeViewport(viewport, rowHeight);
        deltaY += state.Keys.TakeScrollRows() * rowHeight;
        var pending = state.Reveal.Pending;
        if (pending is null)
        {
            ScrollBy(state, rowHeight, viewport, deltaY);
            return;
        }

        ScrollToPending(ui, state, pending, document, rowHeight, advance, textLeft, content, deltaY);
    }

    // A position restored goes before the scroll, so the scroll moves on from it; the others go after it, so they win
    // over it.
    private static void ScrollToPending(Ui<UiState> ui, CodeViewState state, PendingScroll pending, DecompiledDocument document, float rowHeight,
        float advance, float textLeft, UiRect content, float deltaY)
    {
        pending.Switch(
            reveal: reveal =>
            {
                ScrollBy(state, rowHeight, content.Height, deltaY);
                ScrollToRevealed(ui, state, document, rowHeight, content, reveal);
            },
            restore: restore =>
            {
                ScrollToRestored(ui, state, document, rowHeight, content, restore);
                ScrollBy(state, rowHeight, content.Height, deltaY);
            },
            showMatch: match =>
            {
                ScrollBy(state, rowHeight, content.Height, deltaY);
                ScrollToMatch(ui, state, rowHeight, advance, textLeft, content, match);
            },
            showCaret: _ =>
            {
                ScrollBy(state, rowHeight, content.Height, deltaY);
                ShowCaretOnceLaidOut(ui, state, document, rowHeight, advance, textLeft, content);
            });
    }

    private static void ScrollBy(CodeViewState state, float rowHeight, float viewport, float deltaY)
    {
        if (deltaY != 0f)
        {
            state.ScrollY = Math.Clamp(state.ScrollY + deltaY, 0f, MathF.Max(0f, state.Folds.RowCount * rowHeight - viewport));
        }
    }

    private static void ScrollToRestored(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, float rowHeight, UiRect content,
        PendingScroll.Restore restoring)
    {
        var restore = restoring.Position;
        if (content.Height <= 0f)
        {
            ui.RequestUpdate();
            return;
        }

        state.Reveal.Arrive();
        var top = Math.Clamp(restore.TopLine, 0, Math.Max(0, document.Lines.Length - 1));
        RevealFolds(state, top);
        state.ScrollY = GetRowTop(CodeFolding.GetRowOf(state.Folds, top), rowHeight);
        state.ScrollX = restore.ScrollX;
        state.Selection.Select(CodeText.Clamp(document, restore.Anchor), CodeText.Clamp(document, restore.Caret));
    }

    private static void ScrollToRevealed(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, float rowHeight, UiRect content,
        PendingScroll.Reveal reveal)
    {
        var viewport = content.Height;
        if (viewport <= 0f)
        {
            ui.RequestUpdate();
            return;
        }

        var line = Math.Clamp(reveal.Line, 0, Math.Max(0, document.Lines.Length - 1));
        RevealFolds(state, line);
        state.ScrollY = MathF.Max(0f, GetRowTop(CodeFolding.GetRowOf(state.Folds, line), rowHeight) - viewport / 3f);
        state.Reveal.ArriveFlashing(line, ui.RealTime);
        ui.RequestUpdate();
    }

    private static bool IsRowInView(float top, float rowHeight, float scrollY, float viewport)
    {
        return top >= scrollY && top + rowHeight <= scrollY + viewport;
    }

    private static void ScrollToMatch(Ui<UiState> ui, CodeViewState state, float rowHeight, float advance, float textLeft, UiRect content,
        PendingScroll.ShowMatch match)
    {
        var viewport = content.Height;
        if (viewport <= 0f || content.Width <= 0f)
        {
            ui.RequestUpdate();
            return;
        }

        RevealFolds(state, match.Line);
        var top = GetRowTop(CodeFolding.GetRowOf(state.Folds, match.Line), rowHeight);
        if (!IsRowInView(top, rowHeight, state.ScrollY, viewport))
        {
            state.ScrollY = MathF.Max(0f, top - viewport / 3f);
        }

        var visible = content.Width - textLeft - advance;
        var from = match.ColumnStart * advance;
        var to = match.ColumnEnd * advance;
        if (from < state.ScrollX)
        {
            state.ScrollX = MathF.Max(0f, from - 4f * advance);
        }
        else if (to > state.ScrollX + visible)
        {
            state.ScrollX = to - visible + 4f * advance;
        }

        state.Reveal.Arrive();
    }

    private static void ShowCaretOnceLaidOut(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, float rowHeight, float advance,
        float textLeft, UiRect content)
    {
        if (content.Height <= 0f || content.Width <= 0f)
        {
            ui.RequestUpdate();
            return;
        }

        ShowCaret(state, document, rowHeight, advance, textLeft, content);
        state.Reveal.Arrive();
    }

    private static void ShowCaret(CodeViewState state, DecompiledDocument document, float rowHeight, float advance, float textLeft, UiRect content)
    {
        var folds = state.Folds;
        var caret = state.Selection.Caret;
        var row = CodeFolding.GetRowOf(folds, caret.Line);
        var top = GetRowTop(row, rowHeight);
        if (top < state.ScrollY)
        {
            state.ScrollY = top;
        }
        else if (top + rowHeight > state.ScrollY + content.Height)
        {
            state.ScrollY = MathF.Max(0f, top + rowHeight - content.Height);
        }

        var rowLine = CodeFolding.GetLineAt(folds, row);
        if (rowLine != caret.Line)
        {
            return;
        }

        var visible = content.Width - textLeft - advance;
        var x = Math.Min(caret.Column, document.Lines[caret.Line].Text.Length) * advance;
        if (x < state.ScrollX)
        {
            state.ScrollX = MathF.Max(0f, x - 4f * advance);
        }
        else if (x > state.ScrollX + visible)
        {
            state.ScrollX = x - visible + 4f * advance;
        }
    }

    private static CodeReference? FindReferenceAt(DecompiledDocument document, TextPosition at)
    {
        if (at.Line < 0 || at.Line >= document.Lines.Length)
        {
            return null;
        }

        var spans = document.Lines[at.Line].Spans;
        // Or else the span just before it, so that a caret at the end of a name finds it.
        foreach (var column in (ReadOnlySpan<int>)[at.Column, at.Column - 1])
        {
            if (column < 0)
            {
                continue;
            }

            var index = CodeText.SpanAt(spans, column);
            if (index is null)
            {
                continue;
            }

            var reference = spans[index.Value].Reference;
            if (reference != null)
            {
                return reference;
            }
        }
        return null;
    }

    private static CodeReference.Entity? AnalyzableAt(DecompiledDocument document, TextPosition at)
    {
        if (at.Line < 0 || at.Line >= document.Lines.Length)
        {
            return null;
        }

        var spans = document.Lines[at.Line].Spans;
        // Just before it too, as the caret sits after a word as often as in it.
        foreach (var column in (ReadOnlySpan<int>)[at.Column, at.Column - 1])
        {
            if (column < 0)
            {
                continue;
            }

            var index = CodeText.SpanAt(spans, column);
            if (index is null)
            {
                continue;
            }

            var analyzable = AnalyzableIn(spans[index.Value]);
            if (analyzable is not null)
            {
                return analyzable;
            }
        }
        return null;
    }

    // The menu and the key both decide by it, so that they agree on what Analyze applies to.
    private static CodeReference.Entity? AnalyzableIn(DocumentSpan span)
    {
        // Punctuation linked to a member is a plain span: Analyze doesn't apply to it.
        if (span.Kind != SpanKind.Plain && span.Reference is CodeReference.Entity entity)
        {
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
            {
                continue;
            }

            if (keys[k].Key == KeyboardKey.Escape && state.Find.IsOpen)
            {
                state.Find.Close();
                ui.Focus(focusId, takesText: false);
                continue;
            }
            if (focused)
            {
                MoveCaret(state, document, keys[k].Key, keys[k].Modifiers);
            }
        }
    }

    private static void MoveCaret(CodeViewState state, DecompiledDocument document, KeyboardKey key, KeyModifiers modifiers)
    {
        if (document.Lines.Length == 0)
        {
            return;
        }

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
            // Alt with Left or Right goes back and forward outside macOS: it moves nothing here.
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
                state.Keys.ScrollRows(-1);
                return;
            case KeyboardKey.DownArrow when !mac && command:
                state.Keys.ScrollRows(1);
                return;
            case KeyboardKey.UpArrow:
                MoveRows(state, document, caret, -1, extend);
                return;
            case KeyboardKey.DownArrow:
                MoveRows(state, document, caret, 1, extend);
                return;
            case KeyboardKey.PageUp:
                state.Keys.ScrollRows(-state.Layout.PageRows);
                MoveRows(state, document, caret, -state.Layout.PageRows, extend);
                return;
            case KeyboardKey.PageDown:
                state.Keys.ScrollRows(state.Layout.PageRows);
                MoveRows(state, document, caret, state.Layout.PageRows, extend);
                return;
            default:
                return;
        }
        PutCaret(state, to, extend);
    }

    private static void MoveRows(CodeViewState state, DecompiledDocument document, TextPosition caret, int rows, bool extend)
    {
        var goal = state.Keys.Goal;
        var column = goal is not null && goal.Value.At == state.Selection.Caret ? goal.Value.Column : caret.Column;
        var to = CodeCaret.MoveRows(document, state.Folds, caret, rows, column);
        PutCaret(state, to, extend);
        state.Keys.AimAt(new CaretGoal { At = to, Column = column });
    }

    private static void PutCaret(CodeViewState state, TextPosition to, bool extend)
    {
        var selection = state.Selection;
        selection.Select(extend ? selection.Anchor : to, to);
        state.Reveal.AskUnlessAsked(new PendingScroll.ShowCaret());
    }

    private static bool RunShortcut(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, bool focused, int index)
    {
        if (ui.IsPressedAt(Shortcuts.Find, index))
        {
            if (!state.Find.IsOpen && state.HasSelection && state.Selection.Anchor.Line == state.Selection.Caret.Line)
            {
                state.Query = GetSelectedText(state);
            }

            state.Find.OpenToType();
            return true;
        }
        if (state.Query.Length > 0 && (ui.IsPressedAt(Shortcuts.FindNext, index) || ui.IsPressedAt(Shortcuts.FindPrevious, index)))
        {
            state.Find.Open();
            Step(state, document, ui.IsPressedAt(Shortcuts.FindPrevious, index) ? -1 : 1);
            return true;
        }
        if (!focused)
        {
            return false;
        }

        if (ui.IsPressedAt(Shortcuts.ToggleFold, index))
        {
            ToggleFold(state, state.Selection.Caret.Line);
            return true;
        }
        if (ui.IsPressedAt(Shortcuts.ToggleAllFolds, index))
        {
            if (state.Folds.AnyCollapsed)
            {
                ExpandAll(state);
            }
            else
            {
                CollapseAll(state);
            }

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
        if (fold is not null)
        {
            SetCollapsed(state, fold.Value, collapse);
        }
    }

    private static void CopySelection(Ui<UiState> ui, CodeViewState state)
    {
        var style = CopyStyleOf(ui, state.Frame);
        ui.SetClipboardHtml(GetSelectedHtml(state, style), GetSelectedText(state));
    }

    private static CodeHtmlStyle CopyStyleOf(Ui<UiState> ui, CodeViewFrame frame)
    {
        var family = CodeDisplay.GetFontName(ui.State.Display.Font);
        return new CodeHtmlStyle
        {
            Palette = frame.Palette,
            Background = frame.Background,
            ErrorText = frame.ErrorText,
            FontFamily = family,
            FontSize = frame.FontSize,
            LineHeight = frame.RowHeight,
        };
    }

    private static void FindBar(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, int focusId, int findId)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        using (ui.PaneToolbar())
        {
            if (state.Find.TryTakeFocus())
            {
                ui.Focus(findId);
            }

            var submitted = ui.TextField("find", ref state.Query, t.FindPlaceholder, Sizing.Grow(AppLayout.FindMinWidth, AppLayout.FindWidth), clearable: true);
            var queryChanged = !string.Equals(state.Query, state.Find.Index.Query, StringComparison.Ordinal);
            if (state.Find.TryRescan(document, state.Query))
            {
                if (queryChanged)
                {
                    state.Find.SelectFirstMatchFrom(state.Selection.Start);
                }
                else
                {
                    state.Find.ClearMatch();
                }

                ShowMatch(state);
            }
            if (submitted)
            {
                Step(state, document, (ui.Input.Modifiers & KeyModifiers.Shift) != 0 ? -1 : 1);
            }

            var count = state.Find.Index.Matches.Count;
            var label = state.Query.Length == 0 ? ""
                : count == 0 ? t.FindNoResults
                : ui.Memo("count", (state.Find.CurrentMatch, count, t), static a => a.CurrentMatch is not null ? a.t.FindPosition(a.CurrentMatch.Value + 1, a.count) : a.t.FindMatches(a.count));
            ui.Text(label, new Box { Width = Sizing.Grow() }, color: count == 0 && state.Query.Length > 0 ? theme.Warning : theme.TextMuted,
                size: theme.FontSizeSmall, overflow: TextOverflow.Ellipsis);
            var previousTip = ui.Memo("previous-tip", t, static s => s.FindPrevious("Shift+F3"));
            if (ui.IconButton("previous", ui.State.Icons.CodeFindPrevious, previousTip, count > 0))
            {
                Step(state, document, -1);
            }

            var nextTip = ui.Memo("next-tip", t, static s => s.FindNext("F3"));
            if (ui.IconButton("next", ui.State.Icons.CodeFindNext, nextTip, count > 0))
            {
                Step(state, document, 1);
            }

            var closeTip = ui.Memo("close-tip", t, static s => s.FindClose("Esc"));
            if (ui.IconButton("close", ui.State.Icons.CodeFindClose, closeTip))
            {
                state.Find.Close();
                ui.Focus(focusId, takesText: false);
            }
        }
    }

    private static void Step(CodeViewState state, DecompiledDocument document, int direction)
    {
        state.Find.TryRescan(document, state.Query);
        state.Find.Step(direction, state.Selection.Caret);
        ShowMatch(state);
    }

    private static void ShowMatch(CodeViewState state)
    {
        var current = state.Find.CurrentMatch;
        if (current is null)
        {
            return;
        }

        var index = state.Find.Index;
        var match = index.Matches[current.Value];
        state.Selection.Select(match, match with { Column = match.Column + index.Length });
        RevealFolds(state, match.Line);
        state.Reveal.Ask(new PendingScroll.ShowMatch
        {
            Line = match.Line,
            ColumnStart = match.Column,
            ColumnEnd = match.Column + index.Length,
        });
    }

    private static CodeViewIntent? DiagramItems(Ui<UiState> ui, CodeReference.Entity entity, string? why)
    {
        if (entity.Kind != EntityKind.Type)
        {
            return null;
        }

        var t = ui.State.Strings;
        CodeViewIntent? picked = null;
        if (ui.MenuItem(t.TreeShowInDiagram, enabled: why is null, id: "show-in-diagram", detail: why))
        {
            picked = new CodeViewIntent.ShowInDiagram(entity);
        }

        if (ui.MenuItem(t.TreeShowInNewDiagram, enabled: why is null, id: "show-in-new-diagram", detail: why))
        {
            picked = new CodeViewIntent.ShowInNewDiagram(entity);
        }

        return picked;
    }

    private static CodeViewIntent? ContextMenu(Ui<UiState> ui, CodeViewState state, DecompiledDocument document, bool bookmarkable,
        string? closedAssembly)
    {
        var t = ui.State.Strings;
        CodeViewIntent? picked = null;
        using var menu = ui.ContextMenu("menu");
        if (!menu.IsOpen)
        {
            return picked;
        }

        var target = state.Pointer.Menu;

        if (ui.MenuItem(t.CodeCopy, Shortcuts.Copy, enabled: state.HasSelection, id: "copy"))
        {
            CopySelection(ui, state);
        }

        if (ui.MenuItem(t.CodeCopyPlainText, Shortcuts.CopyPlainText, enabled: state.HasSelection, id: "copy-plain-text"))
        {
            ui.ClipboardText = GetSelectedText(state);
        }

        if (ui.MenuItem(t.CodeSelectAll, Shortcuts.SelectAll, id: "select-all"))
        {
            state.Selection.Select(default, CodeText.End(document));
        }

        var line = target.Line;
        if (bookmarkable && line is not null)
        {
            ui.MenuSeparator();
            if (ui.MenuItem(t.CodeToggleBookmark, id: "toggle-bookmark",
                    icon: new UiIcon(ui.State.Icons.CodeToggleBookmark, ui.State.CodeViewPalette.Bookmark)))
            {
                picked = new CodeViewIntent.ToggleBookmark(line.Value);
            }
        }
        if (document.Folds.Length > 0)
        {
            ui.MenuSeparator();
            var fold = line is not null ? CodeFolding.FindInnermost(state.Folds, line.Value) : null;
            if (ui.MenuItem(t.CodeToggleFold, Shortcuts.ToggleFold, id: "toggle-fold", enabled: fold is not null) && line is not null)
            {
                ToggleFold(state, line.Value);
            }

            if (ui.MenuItem(t.CodeCollapseAll, id: "collapse-all"))
            {
                CollapseAll(state);
            }

            if (ui.MenuItem(t.CodeExpandAll, id: "expand-all"))
            {
                ExpandAll(state);
            }
        }
        var why = ClosedText(ui, closedAssembly);
        var reference = target.Navigable;
        if (reference != null)
        {
            ui.MenuSeparator();
            if (ui.MenuItem(t.CodeGoToDefinition, enabled: why is null, id: "go-to-definition", detail: why))
            {
                picked = new CodeViewIntent.Navigate { Target = reference, InNewTab = false };
            }

            if (ui.MenuItem(t.CodeOpenInNewTab, enabled: why is null, id: "open-in-new-tab", detail: why))
            {
                picked = new CodeViewIntent.Navigate { Target = reference, InNewTab = true };
            }
        }
        var entity = target.Entity;
        if (entity != null)
        {
            if (target.Navigable == null)
            {
                ui.MenuSeparator();
            }

            var analyzable = target.Analyzable;
            if (analyzable is not null && ui.MenuItem(t.TreeAnalyze, Shortcuts.Analyze, enabled: why is null, id: "analyze", detail: why))
            {
                picked = new CodeViewIntent.Analyze(analyzable);
            }

            if (ui.MenuItem(t.TreeShowInApiReference, enabled: why is null, id: "show-in-api-reference", icon: new UiIcon(ui.State.Icons.LinkApiReference),
                    detail: why))
            {
                picked = new CodeViewIntent.ShowInApiReference(entity);
            }

            picked = DiagramItems(ui, entity, why) ?? picked;
        }
        var shown = target.Metadata;
        if (shown != null)
        {
            if (target.Navigable == null && target.Entity == null)
            {
                ui.MenuSeparator();
            }

            if (ui.MenuItem(t.TreeShowInMetadata, enabled: why is null, id: "show-in-metadata", detail: why))
            {
                picked = new CodeViewIntent.ShowInMetadata(shown);
            }
        }
        return picked;
    }

    private static string? ClosedText(Ui<UiState> ui, string? closedAssembly)
    {
        if (closedAssembly is null)
        {
            return null;
        }

        return ui.Memo("closed", (closedAssembly, t: ui.State.Strings), static a => a.t.DocumentClosed(a.closedAssembly));
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
        var collapsedFold = fold is not null && CodeFolding.IsCollapsed(frame.Folds, fold.Value) ? fold : null;
        var collapsed = collapsedFold is not null;
        var chipStart = text.Length + 1;
        var chipText = collapsedFold is not null ? frame.Document.Folds[collapsedFold.Value].CollapsedText : "";
        var x0 = r.X + frame.TextLeft;
        var textAreaLeft = left + frame.GutterWidth;
        var firstVisible = Math.Max(0, (int)MathF.Floor((textAreaLeft - x0) / frame.Advance));
        var endVisible = Math.Min(text.Length, (int)MathF.Ceiling((right - x0) / frame.Advance));
        var cellTop = r.Y + MathF.Floor((frame.RowHeight - frame.CellHeight) / 2);

        if (index == frame.CurrentLine)
        {
            p.FillRect(new UiRect(textAreaLeft, r.Y, right - textAreaLeft, r.Height), frame.Palette.CurrentLine);
        }

        if (index == frame.FlashLine)
        {
            p.FillRect(new UiRect(textAreaLeft, r.Y, right - textAreaLeft, r.Height), frame.Flash);
        }

        if (frame.Brackets is not null)
        {
            FillBrackets(p, frame, x0, textAreaLeft, r, index, frame.Brackets.Value);
        }

        if (frame.Highlight != null)
        {
            foreach (var span in line.Spans)
            {
                if (span.Reference != null && span.Reference.Equals(frame.Highlight))
                {
                    FillCells(p, frame, x0, textAreaLeft, r, span.Start, span.End, frame.Palette.Occurrence);
                }
            }
        }

        if (frame.MatchLength > 0)
        {
            for (var m = GetFirstMatchOn(frame, index); m < frame.Matches.Count && frame.Matches[m].Line == index; m++)
            {
                if (m == frame.CurrentMatch)
                {
                    FillCells(p, frame, x0, textAreaLeft, r, frame.Matches[m].Column, frame.Matches[m].Column + frame.MatchLength, frame.Palette.CurrentFind, frame.Palette.CurrentFindOutline, frame.OutlineWidth);
                }
                else
                {
                    FillCells(p, frame, x0, textAreaLeft, r, frame.Matches[m].Column, frame.Matches[m].Column + frame.MatchLength, frame.Palette.FindMatch);
                }
            }
        }

        if (frame.SelectionStart < frame.SelectionEnd && index >= frame.SelectionStart.Line && index <= frame.SelectionEnd.Line)
        {
            var from = index == frame.SelectionStart.Line ? frame.SelectionStart.Column : 0;
            var to = index == frame.SelectionEnd.Line ? frame.SelectionEnd.Column
                : collapsedFold is not null && frame.SelectionEnd.Line > frame.Document.Folds[collapsedFold.Value].EndLine ? chipStart + chipText.Length + 1
                : text.Length + 1;
            FillCells(p, frame, x0, textAreaLeft, r, from, to, frame.Selection);
        }

        if (collapsed)
        {
            var chipLeft = x0 + chipStart * frame.Advance;
            p.FillRect(new UiRect(chipLeft - frame.Advance / 2, cellTop, (chipText.Length + 1) * frame.Advance, frame.CellHeight),
                collapsedFold == frame.HoverChip ? frame.Palette.FoldChipHovered : frame.Palette.FoldChip, frame.ChipRadius);
            p.GlyphRun(new PointF(chipLeft, cellTop), frame.GetCells(chipText, 0, chipText.Length), frame.Advance, collapsedFold == frame.HoverChip ? frame.GutterHover : frame.Palette.GutterText,
                frame.FontSize, family: MonoFonts.Code);
        }

        if (endVisible > firstVisible)
        {
            var spans = line.Spans;
            var error = frame.Document.Error is not null;
            var first = CodeText.SpanAt(spans, firstVisible) ?? 0;
            var underlined = frame.Underline is not null && frame.Underline.Value.Line == index ? frame.Underline.Value.Span : (int?)null;
            for (var s = first; s < spans.Length && spans[s].Start < endVisible; s++)
            {
                var span = spans[s];
                var start = Math.Max(span.Start, firstVisible);
                var end = Math.Min(span.End, endVisible);
                if (end <= start)
                {
                    continue;
                }

                var cells = frame.GetCells(text, start, end);
                p.GlyphRun(new PointF(x0 + start * frame.Advance, cellTop), cells, frame.Advance, error ? frame.ErrorText : frame.Palette[span.Kind],
                    frame.FontSize, family: MonoFonts.Code, underline: s == underlined);
            }
        }

        var caret = frame.Caret;
        if (caret is not null)
        {
            PaintCaret(p, frame, caret.Value, x0, r, index, collapsedFold, chipStart + chipText.Length);
        }

        if (frame.GutterWidth <= 0f)
        {
            return;
        }

        p.FillRect(new UiRect(left, r.Y, frame.GutterWidth, r.Height), frame.Background);
        p.FillRect(new UiRect(left + frame.GutterWidth - frame.SeparatorWidth, r.Y, frame.SeparatorWidth, r.Height), frame.Palette.GutterSeparator);
        if (frame.Digits > 0)
        {
            PaintLineNumber(p, frame, left, cellTop, index);
        }

        if (frame.Bookmarks is not null && (frame.Bookmarks.TryGetValue(index, out var enabled) || index == frame.HoverMark))
        {
            var color = !frame.Bookmarks.ContainsKey(index) ? frame.Palette.GutterText : enabled ? frame.Palette.Bookmark : frame.Palette.GutterText;
            var size = MathF.Min(frame.IconSize, frame.RowHeight);
            p.Glyph(new UiRect(left + (frame.MarkWidth - size) / 2, r.Y + (r.Height - size) / 2, size, size), frame.BookmarkGlyph, color, size);
        }
        if (fold is not null && frame.FoldWidth > 0f)
        {
            PaintChevron(p, frame, left + frame.FoldLeft + frame.FoldWidth / 2, r.Y + r.Height / 2, collapsed, fold == frame.HoverFold ? frame.GutterHover : frame.Palette.GutterText);
        }
    }

    private static void PaintCaret(UiPainter p, CodeViewFrame frame, TextPosition caret, float x0, UiRect row, int index, int? collapsedFold, int chipEnd)
    {
        int column;
        if (caret.Line == index)
        {
            column = caret.Column;
        }
        else if (collapsedFold is not null && caret.Line > index && caret.Line <= frame.Document.Folds[collapsedFold.Value].EndLine)
        {
            column = chipEnd;
        }
        else
        {
            return;
        }

        p.FillRect(new UiRect(x0 + column * frame.Advance, row.Y, frame.CaretWidth, row.Height), frame.CaretColor);
    }

    private static void PaintLineNumber(UiPainter p, CodeViewFrame frame, float left, float cellTop, int index)
    {
        var number = frame.GetNumberCells(index + 1);
        var color = index == frame.CurrentLine ? frame.GutterHover : frame.Palette.GutterText;
        var x = left + frame.GutterPaddingLeft + (frame.Digits - number.Length) * frame.Advance;
        p.GlyphRun(new PointF(x, cellTop), number, frame.Advance, color, frame.FontSize, family: MonoFonts.Code);
    }

    private static void FillBrackets(UiPainter p, CodeViewFrame frame, float x0, float left, UiRect row, int index, BracketPair brackets)
    {
        if (brackets.Open.Line == index)
        {
            FillCells(p, frame, x0, left, row, brackets.Open.Column, brackets.Open.Column + 1, frame.Palette.MatchingBracket);
        }

        if (brackets.Close.Line == index)
        {
            FillCells(p, frame, x0, left, row, brackets.Close.Column, brackets.Close.Column + 1, frame.Palette.MatchingBracket);
        }
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
        {
            p.FillRect(new UiRect(x, row.Y, right - x, row.Height), color, 0f, outline, outlineWidth);
        }
    }

    private static int GetFirstMatchOn(CodeViewFrame frame, int line)
    {
        int low = 0, high = frame.Matches.Count;
        while (low < high)
        {
            var mid = (low + high) >> 1;
            if (frame.Matches[mid].Line < line)
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
