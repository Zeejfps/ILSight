using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Localization;
using ILSight.BuiltInAssets;

namespace ILSight.App.Tests;

public sealed class BuiltInThemeTests
{
    public static TheoryData<string> Ids
    {
        get
        {
            var ids = new TheoryData<string>();
            foreach (var file in BuiltInThemes.Files)
            {
                ids.Add(file.Id.Text);
            }

            return ids;
        }
    }

    [Fact]
    public void The_built_in_themes_load_without_errors_the_default_ones_first()
    {
        var catalog = Theming.CreateCatalog(BuiltInThemes.Files);

        Assert.Empty(BuiltInThemes.Errors);
        Assert.Empty(catalog.Errors);
        Assert.Equal(
            ["dark", "light", "catppuccin-mocha", "dracula", "github-dark", "monokai", "one-dark-pro", "github-light", "solarized-light"],
            catalog.Ids.Select(static i => i.Text));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Every_built_in_theme_keeps_its_text_readable(string id)
    {
        var theme = Resolve(id).Ui;

        Assert.InRange(Contrast(theme.Text, theme.Background), 4.5f, 21f);
        Assert.InRange(Contrast(theme.Text, theme.EditorBackground), 4.5f, 21f);
        Assert.InRange(Contrast(theme.TextMuted, theme.Background), 4.5f, 21f);
        Assert.InRange(Contrast(theme.TextMuted, theme.Surface), 4.5f, 21f);
        Assert.InRange(Contrast(theme.SelectionText, theme.Selection), 4.5f, 21f);
        Assert.InRange(Contrast(theme.Text, theme.TextSelection), 3.5f, 21f);
        Assert.InRange(Contrast(theme.OnAccent, theme.Accent), 4.5f, 21f);
        Assert.InRange(Contrast(theme.AccentText, theme.Background), 4.5f, 21f);
        Assert.Equal(TypeOf(id) == ThemeType.Dark, theme.IsDark);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Every_built_in_theme_shows_selected_text_on_a_selected_row(string id)
    {
        var theme = Resolve(id).Ui;

        Assert.InRange(Contrast(theme.TextSelection, theme.Selection), 1.15f, 21f);
        Assert.InRange(Contrast(theme.TextSelection, theme.SelectionInactive), 1.15f, 21f);
        Assert.InRange(Contrast(theme.TextSelection, theme.Background), 1.15f, 21f);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Every_built_in_theme_colors_every_syntax_kind_and_icon(string id)
    {
        var theme = Resolve(id);

        foreach (var kind in Enum.GetValues<SpanKind>())
        {
            Assert.NotEqual(0u, theme.Code[kind]);
        }

        foreach (var kind in Enum.GetValues<NodeIcon>())
        {
            Assert.NotEqual(0u, NodeIcons.GetColor(theme.Icons, kind));
        }

        Assert.NotEqual(0u, theme.Icons.Restricted);
        Assert.NotEqual(0u, theme.Code.CurrentLine);
        Assert.NotEqual(0u, theme.Code.Bookmark);
    }

    [Fact]
    public void The_default_themes_are_named_in_the_interface_language_and_the_others_by_their_proper_names()
    {
        var catalog = Theming.CreateCatalog(BuiltInThemes.Files);

        var german = catalog.ListNames(Strings.For(Locale.De).Culture);
        var japanese = catalog.ListNames(Strings.For(Locale.Ja).Culture);

        Assert.Equal(["Dunkel", "Hell"], german.Take(2));
        Assert.Equal(["ダーク", "ライト"], japanese.Take(2));
        Assert.Contains("GitHub Dark", german);
        Assert.Contains("GitHub Dark", japanese);
    }

    private static AppTheme Resolve(string id)
    {
        return Theming.CreateCatalog(BuiltInThemes.Files).GetTheme(ThemeId<ThemeFile>.Of(id));
    }

    private static ThemeType? TypeOf(string id)
    {
        return BuiltInThemes.Files.Single(f => f.Id.Text == id).Type;
    }

    private static float Contrast(uint a, uint b)
    {
        var (la, lb) = (UiColor.Luminance(a), UiColor.Luminance(b));
        return (Math.Max(la, lb) + 0.05f) / (Math.Min(la, lb) + 0.05f);
    }
}
