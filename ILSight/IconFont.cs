using ZGF.Fonts;
using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>A font an icon theme file declares by the path it names: loaded, or broken when it could not
/// be read or loaded, which was reported.</summary>
public union IconFont(IconFont.Loaded, IconFont.Broken)
{
    /// <summary>The face read from the font and the characters it has glyphs for.</summary>
    public sealed record Loaded
    {
        public required string Path { get; init; }

        public required FontFace Face { get; init; }

        public required FontCharacters Characters { get; init; }
    }

    public sealed record Broken
    {
        public required string Path { get; init; }
    }
}
