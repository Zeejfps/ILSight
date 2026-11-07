namespace ILSight;

/// <summary>The color themes the app can be drawn in, by id: the built-in ones, then the user's. One that
/// cannot be resolved falls back to its parent, else to the built-in theme of its type.</summary>
public sealed class ThemeCatalog : ThemeCatalog<ThemeFile, ThemeContents, AppTheme>
{
    public static readonly ThemeId<ThemeFile> DarkId = ThemeId<ThemeFile>.Of("dark");
    public static readonly ThemeId<ThemeFile> LightId = ThemeId<ThemeFile>.Of("light");
}
