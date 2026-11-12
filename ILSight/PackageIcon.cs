namespace ILSight;

/// <summary>A package's icon: the image the screen draws it as, and what it shows once its pixels are in.</summary>
public sealed class PackageIcon
{
    public required string ImageId { get; init; }

    /// <summary>The icon's pixels as drawn; null until they are in.</summary>
    public ShownIcon? Shown { get; set; }
}
