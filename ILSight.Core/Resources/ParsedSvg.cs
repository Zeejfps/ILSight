using ZGF.Svg;

namespace ILSight.Core.Resources;

/// <summary>An SVG document as ZGF.Svg parsed it (<see cref="SvgPicture.Parse"/>), with its own size as read from it,
/// which may be none (zero).</summary>
internal sealed record ParsedSvg
{
    public required SvgDocument Document { get; init; }

    public required float Width { get; init; }

    public required float Height { get; init; }
}
