using ZGF.Fonts;
using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>A font an icon theme file declares by the path it names: loaded, or broken when it could not
/// be read or loaded, which was reported.</summary>
public abstract record IconFont
{
    private IconFont()
    {
    }

    public required string Path { get; init; }

    /// <summary>The face read from the font and the characters it has glyphs for.</summary>
    public sealed record Loaded : IconFont
    {
        public required FontFace Face { get; init; }

        public required FontCharacters Characters { get; init; }
    }

    public sealed record Broken : IconFont;
}
