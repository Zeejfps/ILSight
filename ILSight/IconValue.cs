using ZGF.Svg;

namespace ILSight;

/// <summary>An icon as an icon theme file writes it, with the file and line it is written on: a glyph of
/// a font, or a picture read from an SVG file.</summary>
public union IconValue(IconValue.Glyph, IconValue.Picture)
{
    /// <summary>The codepoint <see cref="Codepoint"/> of the font the theme declares as <see cref="Font"/>.</summary>
    public sealed record Glyph
    {
        public required string File { get; init; }

        public required int Line { get; init; }

        public required string Font { get; init; }

        public required int Codepoint { get; init; }
    }

    /// <summary>The picture <see cref="Svg"/> read from the file at <see cref="SvgPath"/>.</summary>
    public sealed record Picture
    {
        public required string File { get; init; }

        public required int Line { get; init; }

        public required string SvgPath { get; init; }

        public required SvgDocument Svg { get; init; }
    }
}
