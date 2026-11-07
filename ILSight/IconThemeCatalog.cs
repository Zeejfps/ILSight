namespace ILSight;

/// <summary>The icon themes the app can be drawn with, by id: the built-in ones, then the user's. Every
/// theme can draw in the reference theme's fonts, and a key no theme in its chain draws is drawn as the
/// reference theme draws it. One that cannot be resolved falls back to its parent, else to the reference
/// theme.</summary>
public sealed class IconThemeCatalog : ThemeCatalog<IconThemeFile, IconThemeContents, IconTheme>
{
    public static readonly ThemeId<IconThemeFile> ReferenceId = ThemeId<IconThemeFile>.Of("lucide");
}
