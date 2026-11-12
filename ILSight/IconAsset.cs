namespace ILSight;

/// <summary>A font or picture file an icon theme names: its bytes, or why it cannot be read.</summary>
public readonly record struct IconAsset(byte[]? Bytes, ThemeProblem Problem = ThemeProblem.AssetUnreadable, string Detail = "");
