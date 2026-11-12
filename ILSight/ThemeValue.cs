namespace ILSight;

/// <summary>A color as a theme file writes it, a <c>#RRGGBB</c> or <c>#RRGGBBAA</c> color or a palette
/// name, with the file and line it is written on.</summary>
public readonly record struct ThemeValue(string Text, string File, int Line)
{
    public bool IsPaletteName => !Text.StartsWith(ThemeFile.ColorPrefix);
}
