using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.BuiltInAssets;

namespace ILSight.CodeView;

/// <summary>What every row of one frame of a code view is painted from, refilled each frame.</summary>
internal sealed class CodeViewFrame
{
    public int[] CodePoints = new int[256];

    public DecompiledDocument Document = DecompiledDocument.Empty;
    /// <summary>The folds as the rows of this frame were counted, which go on holding for them while they
    /// are painted, should the view's change meanwhile, as a menu's item does.</summary>
    public CodeFoldLayout Folds = CodeFoldLayout.None;
    public CodeViewPalette Palette = BuiltInThemes.Dark.Code;
    public uint Background;
    public uint GutterHover;
    public uint Selection;
    public uint ErrorText;
    public uint Flash;
    public float FontSize;
    public float SeparatorWidth;
    public float OutlineWidth;
    public float ChipRadius;
    public float GutterPaddingLeft;
    public float Advance;
    public float RowHeight;
    public float CellHeight;
    public float FoldLeft;
    public float FoldWidth;
    public float GutterWidth;
    public float TextLeft;
    public int Digits;
    public TextPosition SelectionStart;
    public TextPosition SelectionEnd;
    public CodeReference? Highlight;
    public CodeSpanAt? Underline;
    public IReadOnlyList<TextPosition> Matches = [];
    public int MatchLength;
    public int? CurrentMatch;
    public int? FlashLine;
    public int? HoverFold;
    public int? HoverChip;
    public float MarkWidth;
    public float IconSize;
    public IconGlyph BookmarkGlyph;
    public IReadOnlyDictionary<int, bool>? Bookmarks;
    public int? HoverMark;

    /// <summary>The caret's line when it is highlighted, else -1.</summary>
    public int? CurrentLine;

    /// <summary>The bracket at the caret and its match when they are highlighted.</summary>
    public BracketPair? Brackets;

    /// <summary>The caret while it shows, in a focused view and the on half of its blink; else null.</summary>
    public TextPosition? Caret;
    public uint CaretColor;
    public float CaretWidth;
}
