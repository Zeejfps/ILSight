using ZGF.Fonts;
using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>A font an icon theme file declares by the path it names: loaded, or broken when it could not
/// be read or loaded, which was reported.</summary>
public abstract record IconFont
{
    private IconFont(string path)
    {
        Path = path;
    }

    public string Path { get; }

    /// <summary>The face read from the font and the characters it has glyphs for.</summary>
    public sealed record Loaded(string Path, FontFace Face, FontCharacters Characters) : IconFont(Path);

    public sealed record Broken(string Path) : IconFont(Path);
}
