namespace ILSight;

/// <summary>A font or picture file a theme names: its bytes, or why it cannot be read; and, for a font, the family its
/// source draws it as, or null to draw it as the family of the id the theme gives it.</summary>
public readonly record struct IconAsset
{
    public required byte[]? Bytes { get; init; }

    public ThemeProblem Problem { get; init; } = ThemeProblem.AssetUnreadable;

    public string Detail { get; init; } = "";

    public string? Family { get; init; }

    public IconAsset()
    {
    }
}
