using ZGF.Svg;

namespace ILSight;

/// <summary>An icon as an icon theme file writes it, with the file and line it is written on: a glyph of
/// a font, or a picture read from an SVG file.</summary>
public abstract record IconValue
{
    private IconValue(string file, int line)
    {
        File = file;
        Line = line;
    }

    public string File { get; }

    public int Line { get; }

    /// <summary>The codepoint <see cref="Codepoint"/> of the font the theme declares as <see cref="Font"/>.</summary>
    public sealed record Glyph(string Font, int Codepoint, string File, int Line) : IconValue(File, Line);

    /// <summary>The picture <see cref="Svg"/> read from the file at <see cref="SvgPath"/>.</summary>
    public sealed record Picture(string SvgPath, SvgDocument Svg, string File, int Line) : IconValue(File, Line);
}
