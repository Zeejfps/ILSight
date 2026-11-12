using ILSight.Core.Resources;

namespace ILSight;

/// <summary>An icon's pixels, drawn as the image <paramref name="ImageId"/>, with whether they are dark
/// marks on a clear background, which a dark theme would hide.</summary>
public sealed record ShownIcon(string ImageId, ResourceImage Pixels, bool IsDarkOnClear);
