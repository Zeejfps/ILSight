using System.Globalization;
using System.Text;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.CodeViewArea;
using ILSight.BuiltInArea;
using ILSight.AppSettingArea;
using ILSight.SettingArea;

namespace ILSight.App.Tests;

public sealed class UserThemeTests : IDisposable
{
    private readonly TempDir _temp = new();

    private static ThemeId<ThemeFile> Id(string text)
    {
        return ThemeId<ThemeFile>.Of(text);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    private string WriteTheme(string id, string json)
    {
        return _temp.Write(Path.Combine("themes", id + ".json"), json);
    }

    private AppSettings Settings()
    {
        var settings = _temp.Settings();
        settings.Locale = Locale.En;
        return settings;
    }

    private static string? CreateCustomTheme(AppState appState)
    {
        var themes = appState.Themes;
        var custom = appState.UserThemes.CreateCustomTheme(appState.Theme, parent => Strings.En.ThemeCustomName(themes.GetThemeName(parent, Strings.En.Culture)));
        if (custom is null)
        {
            return null;
        }

        appState.ChosenThemes.ChooseTheme(custom.Id);
        return custom.Path;
    }

    [Theory]
    [InlineData("GitHubDark", "github-dark")]
    [InlineData("SolarizedLight", "solarized-light")]
    [InlineData("Dark", "dark")]
    [InlineData("my-theme", "my-theme")]
    public void A_saved_theme_loads_as_the_id_of_its_file(string saved, string id)
    {
        var path = _temp.Write("settings.json", $$"""{ "Theme": "{{saved}}" }""");

        Assert.Equal(id, AppRoot.ReadSettingsFile(FullFilePath.Of(path)).Settings.Theme.Text);
    }

    [Theory]
    [InlineData("../dracula")]
    [InlineData("a/b")]
    [InlineData("..")]
    [InlineData(" ")]
    public void A_saved_theme_that_names_no_file_loads_as_the_default(string saved)
    {
        var path = _temp.Write("settings.json", $$"""{ "Theme": "{{saved}}" }""");

        Assert.Equal("dark", AppRoot.ReadSettingsFile(FullFilePath.Of(path)).Settings.Theme.Text);
    }

    [Fact]
    public void A_missing_theme_loads_as_the_default()
    {
        var path = _temp.Write("settings.json", """{ "Theme": null }""");

        Assert.Equal("dark", AppRoot.ReadSettingsFile(FullFilePath.Of(path)).Settings.Theme.Text);
    }

    [Fact]
    public void User_themes_in_the_folder_beside_the_settings_are_listed_after_the_built_in_ones()
    {
        WriteTheme("my-dracula", """{ "name": "My Dracula", "extends": "dracula", "ui": { "accent": "#FF0000" } }""");
        var appState = AppRoot.RestoreAppState(Settings(), TestSession.Shared);

        Assert.Equal("my-dracula", appState.Themes.Ids[^1].Text);
        Assert.Equal("My Dracula", appState.Themes.ListNames(CultureInfo.InvariantCulture).Last());
        Assert.Empty(appState.Errors.Entries);
    }

    [Fact]
    public void A_chosen_theme_whose_file_is_gone_shows_dark_and_stays_chosen()
    {
        var settings = Settings();
        settings.Theme = Id("gone");
        var appState = AppRoot.RestoreAppState(settings, TestSession.Shared);

        Assert.Same(appState.Themes.GetTheme(Id("dark")), Theming.ResolveTheme(appState));
        Assert.IsType<SettingsWriteResult.Written>(appState.SettingsSaving.Save(appState));
        Assert.Equal("gone", _temp.Settings().Theme.Text);
    }

    [Fact]
    public void A_problem_in_a_theme_is_reported_once_until_it_changes()
    {
        WriteTheme("broken", """{ "extends": "dark", "ui": { "accent": "purple" } }""");
        var appState = AppRoot.RestoreAppState(Settings(), TestSession.Shared);
        var error = Assert.Single(appState.Errors.Entries);

        appState.UserThemes.ReloadThemes();
        Assert.Single(appState.Errors.Entries);

        WriteTheme("broken", """{ "extends": "dark", "ui": { "accent": "violet" } }""");
        appState.UserThemes.ReloadThemes();

        Assert.Equal("Problems in the theme broken.json", error.Words().Title);
        Assert.EndsWith("broken.json(1): ui.accent: the palette has no color named purple", error.Words().Details);
        Assert.Equal(2, appState.Errors.Entries.Count);
    }

    [Fact]
    public void The_themes_folder_is_made_with_the_schema_and_a_readme_that_are_no_themes()
    {
        var appState = AppRoot.RestoreAppState(Settings(), TestSession.Shared);

        var folder = appState.UserThemes.PrepareFolder();
        appState.UserThemes.ReloadThemes();

        Assert.Equal(_temp.PathOf("themes"), folder);
        Assert.NotNull(folder);
        Assert.Equal(BuiltInThemes.ReadSchema(), File.ReadAllText(Path.Combine(folder, "theme.schema.json")));
        Assert.Equal(BuiltInThemes.ReadGuide(), File.ReadAllText(Path.Combine(folder, "README.md")));
        Assert.Equal(BuiltInThemes.Files.Count, appState.Themes.Ids.Count);
        Assert.Empty(appState.Errors.Entries);
    }

    [Fact]
    public void A_custom_theme_extends_the_one_shown_lists_every_color_and_is_switched_to()
    {
        var settings = Settings();
        settings.Theme = Id("dracula");
        var appState = AppRoot.RestoreAppState(settings, TestSession.Shared);
        var dracula = appState.Themes.GetTheme(Id("dracula"));

        var path = CreateCustomTheme(appState);

        Assert.NotNull(path);
        var text = File.ReadAllText(path);
        Assert.Equal(_temp.PathOf(Path.Combine("themes", "dracula-custom.json")), path);
        Assert.Equal("dracula-custom", appState.Theme.Text);
        Assert.Empty(appState.Errors.Entries);
        Assert.Equal("Dracula (custom)", appState.Themes.GetThemeName(Id("dracula-custom"), CultureInfo.GetCultureInfo("en")));
        Assert.Equal(dracula.Ui, Theming.ResolveTheme(appState).Ui);
        Assert.Equal(dracula.Code.Keyword, Theming.ResolveTheme(appState).Code.Keyword);
        Assert.Contains("\"extends\": \"dracula\",", text);
        Assert.Contains("// \"rowHover\": \"#2C2E3B\",", text);
        Assert.Contains("// \"popupShadowColor\": \"#00000066\",", text);
        Assert.Contains("// \"keyword\": \"#FF79C6\",", text);
        Assert.Contains("// \"restricted\": \"#F1FA8C\",", text);
        Assert.Equal(CountKeys(), CountCommentedColors(text));
    }

    private static int CountKeys()
    {
        return UiTheme.ColorKeys.Count + CodeViewPalette.ColorKeys.Count + NodeIconPalette.ColorKeys.Count;
    }

    private static int CountCommentedColors(string text)
    {
        return text.Split('\n').Count(IsCommentedColor);
    }

    private static bool IsCommentedColor(string line)
    {
        return line.TrimStart().StartsWith("// \"", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("solarized-light")]
    public void Each_color_of_a_custom_theme_can_be_uncommented_alone(string parent)
    {
        var catalog = Theming.CreateCatalog(BuiltInThemes.Files);
        var lines = ThemeDraft.Compose("Mine", Id(parent), catalog.GetTheme(Id(parent))).Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            if (!IsCommentedColor(lines[i]))
            {
                continue;
            }

            var edited = lines.ToArray();
            edited[i] = lines[i].Replace("// ", "");
            var file = Theming.ReadTheme(Id("mine"), "mine.json", Encoding.UTF8.GetBytes(string.Join('\n', edited)));
            catalog.ReplaceUserThemes([file]);
            Assert.True(catalog.Errors.Count == 0, edited[i] + ": " + string.Join("; ", catalog.Errors));
            Assert.Equal(catalog.GetTheme(Id(parent)).Ui.ToColors(), catalog.GetTheme(Id("mine")).Ui.ToColors());
        }
    }

    [Fact]
    public void A_custom_theme_takes_the_next_number_while_its_id_is_taken()
    {
        WriteTheme("dark-custom", "{}");
        WriteTheme("dark-custom-2", "{}");
        var appState = AppRoot.RestoreAppState(Settings(), TestSession.Shared);

        var path = CreateCustomTheme(appState);

        Assert.Equal("dark-custom-3.json", Path.GetFileName(path));
        Assert.Equal("dark-custom-3", appState.Theme.Text);
    }

    [Fact]
    public void A_custom_theme_of_a_theme_that_falls_back_extends_the_one_shown()
    {
        WriteTheme("broken", """{ "type": "light", "extends": "nope" }""");
        var settings = Settings();
        settings.Theme = Id("broken");
        var appState = AppRoot.RestoreAppState(settings, TestSession.Shared);

        var path = CreateCustomTheme(appState);

        Assert.Equal("light-custom.json", Path.GetFileName(path));
        Assert.NotNull(path);
        Assert.Contains("\"extends\": \"light\",", File.ReadAllText(path));
    }

    [Fact]
    public void A_custom_theme_that_cannot_be_written_is_reported()
    {
        var appState = AppRoot.RestoreAppState(Settings(), TestSession.Shared);
        Directory.CreateDirectory(_temp.PathOf(Path.Combine("themes", "dark-custom.json")));

        var path = CreateCustomTheme(appState);

        Assert.Null(path);
        Assert.Equal("dark", appState.Theme.Text);
        Assert.Equal("Cannot write the custom theme", Assert.Single(appState.Errors.Entries).Words().Title);
    }

    [Fact]
    public void No_custom_theme_is_made_while_the_settings_live_in_memory()
    {
        var appState = AppRoot.RestoreAppState(new AppSettings { Locale = Locale.En }, TestSession.Shared);

        Assert.Null(CreateCustomTheme(appState));
        Assert.Equal("dark", appState.Theme.Text);
    }

    [Fact]
    public void A_themes_folder_that_cannot_be_made_is_reported_and_left_unwatched()
    {
        _temp.Write("themes", "not a folder");

        var d = new AppStateDriver(Settings());

        var error = Assert.Single(d.AppState.Errors.Entries);
        Assert.Equal("Cannot make the themes folder", error.Words().Title);
    }

    [Fact]
    public void A_removed_themes_folder_is_made_again_and_watched()
    {
        var settings = Settings();
        settings.Theme = Id("back");
        var d = new AppStateDriver(settings);
        var folder = _temp.PathOf("themes");

        Directory.Delete(folder, recursive: true);
        d.RunUntil(() => Directory.Exists(folder));
        WriteTheme("back", """{ "extends": "dark", "ui": { "accent": "#00FF00" } }""");
        d.RunUntil(() => Theming.ResolveTheme(d.AppState).Ui.Accent == 0xFF00FF00);
        d.AppState.Workflows.CancelAll();

        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void Saving_the_chosen_themes_file_shows_it_at_once()
    {
        var settings = Settings();
        settings.Theme = Id("live");
        var d = new AppStateDriver(settings);
        Assert.Same(d.AppState.Themes.GetTheme(Id("dark")), Theming.ResolveTheme(d.AppState));

        WriteTheme("live", """{ "extends": "dark", "ui": { "accent": "#FF00FF" } }""");
        d.RunUntil(() => Theming.ResolveTheme(d.AppState).Ui.Accent == 0xFFFF00FF);
        d.AppState.Workflows.CancelAll();

        Assert.Equal("live", d.AppState.Theme.Text);
    }
}
