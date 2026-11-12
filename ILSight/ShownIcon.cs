using ILSight.Core.Resources;

namespace ILSight;

/// <summary>An icon's pixels, drawn as the image <see cref="ImageId"/>, with whether they are dark
/// marks on a clear background, which a dark theme would hide.</summary>
public sealed record ShownIcon
{
    public required string ImageId { get; init; }

    public required ResourceImage Pixels { get; init; }

    public required bool IsDarkOnClear { get; init; }
}
