using ZGF.Svg;

namespace ILSight.Core.Resources;

/// <summary>A resource decoded to pixels: straight-alpha RGBA8, rows top-down, and the format
/// it was stored in. An SVG keeps its shapes too, to be drawn again at another size.</summary>
public sealed class ResourceImage
{
    /// <summary>The shapes it is drawn from, for a picture that is drawn from shapes.</summary>
    public SvgDocument? Shapes { get; internal init; }

    /// <summary>Whether it is drawn from shapes, so stays sharp at any size, and is stored as
    /// markup that can be shown instead.</summary>
    public bool IsVector => Shapes is not null;

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required byte[] Rgba { get; init; }

    /// <summary>What it was stored as: PNG, JPEG, BMP, GIF, ICO, CUR or SVG.</summary>
    public required string Format { get; init; }
}
