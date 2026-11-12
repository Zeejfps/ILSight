namespace ILSight;

/// <summary>A color as a theme file writes it, a <c>#RRGGBB</c> or <c>#RRGGBBAA</c> color or a palette
/// name, with the file and line it is written on.</summary>
public readonly record struct ThemeValue
{
    public required string Text { get; init; }

    public required string File { get; init; }

    public required int Line { get; init; }

    public bool IsPaletteName => !Text.StartsWith(ThemeFile.ColorPrefix);
}
