namespace ILSight;

/// <summary>What a readable icon theme file sets.</summary>
/// <param name="Fonts">The fonts the icons can be drawn in, by id.</param>
/// <param name="Icons">The icons, by key.</param>
public sealed record IconThemeContents(IReadOnlyDictionary<string, IconFont> Fonts, IReadOnlyDictionary<string, IconValue> Icons);
