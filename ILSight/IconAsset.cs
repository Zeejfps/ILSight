namespace ILSight;

/// <summary>A font or picture file a theme names: its bytes, or why it cannot be read; and, for a font, the family its
/// source draws it as, or null to draw it as the family of the id the theme gives it.</summary>
public readonly record struct IconAsset(byte[]? Bytes, ThemeProblem Problem = ThemeProblem.AssetUnreadable, string Detail = "", string? Family = null);
