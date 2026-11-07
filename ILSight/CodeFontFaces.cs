using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>The faces code is drawn with, as <see cref="MonoFonts.Code"/>: the bundled one, and each
/// installed font chosen this session, read from its file once.</summary>
internal sealed class CodeFontFaces
{    public readonly Dictionary<CodeFont, FontFace> ReadFaces = new();

    public required FontFace Bundled { get; init; }
}
