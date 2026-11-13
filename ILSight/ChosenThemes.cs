namespace ILSight;

/// <summary>The color and icon themes the user chose, by id, kept even while no theme has it.</summary>
public sealed class ChosenThemes
{
    private ThemeId<ThemeFile>? _theme;
    private ThemeId<IconThemeFile>? _iconTheme;

    public required ThemeId<ThemeFile> Theme
    {
        get => _theme ?? throw new InvalidOperationException("The themes are chosen with a color theme.");
        init => _theme = value;
    }

    public required ThemeId<IconThemeFile> IconTheme
    {
        get => _iconTheme ?? throw new InvalidOperationException("The themes are chosen with an icon theme.");
        init => _iconTheme = value;
    }

    public void ChooseTheme(ThemeId<ThemeFile> theme)
    {
        _theme = theme;
    }

    public void ChooseIconTheme(ThemeId<IconThemeFile> iconTheme)
    {
        _iconTheme = iconTheme;
    }
}
