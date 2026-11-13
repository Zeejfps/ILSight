using ZGF.Gui.Immediate;
using ILSight.BuiltInArea;
using ILSight.CodeViewArea;

namespace ILSight;

/// <summary>What every view is drawn with this frame: the theme's colors, the icons and the fonts they are drawn in,
/// and the strings of the chosen language.</summary>
public sealed class Appearance
{
    public UiTheme Theme { get; private set; } = UiTheme.Dark;

    public CodeViewPalette CodeViewPalette { get; private set; } = BuiltInThemes.Dark.Code;

    public NodeIconPalette NodeIconPalette { get; private set; } = BuiltInThemes.Dark.Icons;

    /// <summary>What each icon key draws in the chosen icon theme.</summary>
    public AppIcons Icons { get; private set; } = BuiltInIconThemes.Reference.Icons;

    /// <summary>The fonts <see cref="Icons"/> are drawn in, which must be registered for them to show.</summary>
    public IReadOnlyList<FontFace> IconFonts { get; private set; } = BuiltInIconThemes.Reference.Fonts;

    public Strings Strings { get; private set; } = Strings.En;

    /// <summary>Takes the colors of <paramref name="theme"/> and the strings of <paramref name="locale"/>.</summary>
    public void ApplyThemeAndLanguage(AppTheme theme, Locale locale)
    {
        Theme = theme.Ui;
        CodeViewPalette = theme.Code;
        NodeIconPalette = theme.Icons;
        Strings = Strings.For(locale);
    }

    /// <summary>Takes the icons of <paramref name="iconTheme"/> and the fonts they are drawn in.</summary>
    public void ApplyIconTheme(IconTheme iconTheme)
    {
        Icons = iconTheme.Icons;
        IconFonts = iconTheme.Fonts;
    }
}
