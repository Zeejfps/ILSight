using ZGF.Gui;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.BuiltInArea;

namespace ILSight.CodeViewArea;

/// <summary>What every row of one frame of a code view is painted from, refilled each frame. One object a view
/// keeps and fills again each frame through its methods, so that a frame allocates nothing for it.</summary>
internal sealed class CodeViewFrame
{
    private int[] _codePoints = new int[256];

    public DecompiledDocument Document { get; private set; } = DecompiledDocument.Empty;

    /// <summary>The folds as the rows of this frame were counted, which go on holding for them while they
    /// are painted, should the view's change meanwhile, as a menu's item does.</summary>
    public CodeFoldLayout Folds { get; private set; } = CodeFoldLayout.None;

    public CodeViewPalette Palette { get; private set; } = BuiltInThemes.Dark.Code;
    public uint Background { get; private set; }
    public uint GutterHover { get; private set; }
    public uint Selection { get; private set; }
    public uint ErrorText { get; private set; }
    public uint Flash { get; private set; }
    public float FontSize { get; private set; }
    public float SeparatorWidth { get; private set; }
    public float OutlineWidth { get; private set; }
    public float ChipRadius { get; private set; }
    public float GutterPaddingLeft { get; private set; }
    public float Advance { get; private set; }
    public float RowHeight { get; private set; }
    public float CellHeight { get; private set; }
    public float FoldLeft { get; private set; }
    public float FoldWidth { get; private set; }
    public float GutterWidth { get; private set; }
    public float TextLeft { get; private set; }
    public int Digits { get; private set; }
    public TextPosition SelectionStart { get; private set; }
    public TextPosition SelectionEnd { get; private set; }
    public CodeReference? Highlight { get; private set; }
    public CodeSpanAt? Underline { get; private set; }
    public IReadOnlyList<TextPosition> Matches { get; private set; } = [];
    public int MatchLength { get; private set; }
    public int? CurrentMatch { get; private set; }
    public int? FlashLine { get; private set; }
    public int? HoverFold { get; private set; }
    public int? HoverChip { get; private set; }
    public float MarkWidth { get; private set; }
    public float IconSize { get; private set; }
    public IconGlyph BookmarkGlyph { get; private set; }
    public IReadOnlyDictionary<int, bool>? Bookmarks { get; private set; }
    public int? HoverMark { get; private set; }

    /// <summary>The caret's line when it is highlighted, else -1.</summary>
    public int? CurrentLine { get; private set; }

    /// <summary>The bracket at the caret and its match when they are highlighted.</summary>
    public BracketPair? Brackets { get; private set; }

    /// <summary>The caret while it shows, in a focused view and the on half of its blink; else null.</summary>
    public TextPosition? Caret { get; private set; }
    public uint CaretColor { get; private set; }
    public float CaretWidth { get; private set; }

    /// <summary>Lays the frame out: <paramref name="document"/> folded as <paramref name="folds"/> says, in
    /// <paramref name="theme"/> and <paramref name="palette"/>, its cells <paramref name="cell"/> in rows
    /// <paramref name="rowHeight"/> tall after <paramref name="gutter"/>; its selection drawn as focused when
    /// <paramref name="active"/>.</summary>
    public void Lay(DecompiledDocument document, CodeFoldLayout folds, CodeViewPalette palette, UiTheme theme, CellMetrics cell, float rowHeight,
        CodeGutter gutter, IconGlyph bookmarkGlyph, bool active)
    {
        Document = document;
        Folds = folds;
        Palette = palette;
        Background = theme.EditorBackground;
        GutterHover = theme.Text;
        Selection = active ? theme.TextSelection : theme.SelectionInactive;
        CaretColor = theme.Text;
        CaretWidth = theme.CaretWidth;
        ErrorText = theme.Danger;
        SeparatorWidth = theme.SeparatorWidth;
        OutlineWidth = theme.BorderWidth;
        ChipRadius = theme.RadiusSmall;
        GutterPaddingLeft = gutter.MarkWidth;
        MarkWidth = gutter.MarkWidth;
        IconSize = theme.IconSize;
        BookmarkGlyph = bookmarkGlyph;
        Advance = cell.Advance;
        CellHeight = cell.Height;
        RowHeight = rowHeight;
        FoldLeft = gutter.FoldLeft;
        FoldWidth = gutter.FoldWidth;
        GutterWidth = gutter.Width;
        TextLeft = gutter.TextLeft;
        Digits = gutter.Digits;
    }

    /// <summary>Marks what the frame shows over the text: the selection from <paramref name="selectionStart"/> to
    /// <paramref name="selectionEnd"/>, the usages of <paramref name="highlight"/>, <paramref name="underline"/>,
    /// what <paramref name="hover"/> is over, and the lines <paramref name="bookmarks"/> marks.</summary>
    public void Mark(TextPosition selectionStart, TextPosition selectionEnd, CodeReference? highlight, CodeSpanAt? underline, CodeHover hover,
        IReadOnlyDictionary<int, bool>? bookmarks)
    {
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
        Highlight = highlight;
        Underline = underline;
        HoverFold = hover.Chevron;
        HoverChip = hover.Chip;
        HoverMark = hover.Mark;
        Bookmarks = bookmarks;
    }

    /// <summary>Shows <paramref name="matches"/> of the find, <paramref name="length"/> long (0 shows none), the one
    /// at <paramref name="current"/> as the current one.</summary>
    public void ShowMatches(IReadOnlyList<TextPosition> matches, int length, int? current)
    {
        Matches = matches;
        MatchLength = length;
        CurrentMatch = current;
    }

    /// <summary>Flashes <paramref name="line"/> in <paramref name="color"/>; no line when it is null.</summary>
    public void ShowFlash(int? line, uint color)
    {
        FlashLine = line;
        if (line is null)
        {
            return;
        }

        Flash = color;
    }

    public void ShowCaret(TextPosition? caret)
    {
        Caret = caret;
    }

    /// <summary>Shows the text at <paramref name="fontSize"/>, with what the display draws around it: the caret's line
    /// and the brackets matching at the caret, each when it is shown.</summary>
    public void ShowAids(float fontSize, int? currentLine, BracketPair? brackets)
    {
        FontSize = fontSize;
        CurrentLine = currentLine;
        Brackets = brackets;
    }

    /// <summary>The code points of <paramref name="text"/> from <paramref name="start"/> to <paramref name="end"/>, one
    /// per cell: a pair of surrogates one code point and a space after it, a lone surrogate a space. Valid until the
    /// next call.</summary>
    public ReadOnlySpan<int> GetCells(string text, int start, int end)
    {
        var count = end - start;
        if (_codePoints.Length < count)
        {
            _codePoints = new int[Math.Max(count, _codePoints.Length * 2)];
        }

        for (var i = 0; i < count; i++)
        {
            var at = start + i;
            var c = text[at];
            if (char.IsHighSurrogate(c) && at + 1 < text.Length && char.IsLowSurrogate(text[at + 1]))
            {
                _codePoints[i] = char.ConvertToUtf32(c, text[at + 1]);
                if (i + 1 < count)
                {
                    _codePoints[++i] = ' ';
                }
            }
            else
            {
                _codePoints[i] = char.IsSurrogate(c) ? ' ' : c;
            }
        }
        return _codePoints.AsSpan(0, count);
    }

    /// <summary>The digits of <paramref name="value"/>, one per cell. Valid until the next call.</summary>
    public ReadOnlySpan<int> GetNumberCells(int value)
    {
        var digits = CodeText.Digits(value);
        var cells = _codePoints.AsSpan(0, digits);
        for (var i = digits - 1; i >= 0; i--)
        {
            cells[i] = '0' + value % 10;
            value /= 10;
        }
        return cells;
    }
}
