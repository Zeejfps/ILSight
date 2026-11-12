using System.Globalization;
using System.Text;
using ILSight.Core;
using static ILSight.App.Tests.BuiltInGlyphs;
using static ILSight.IconTheming;
using static ILSight.ScreenLayout;
using static ILSight.SettingsKeeping.Settings;
using ILSight.BuiltInAssets;
using ILSight.UserState;
using ILSight.AppSettingsFormat;
using ILSight.DisplayChoices;

namespace ILSight.App.Tests;

public sealed class UserIconThemeTests : IDisposable
{
    private const string ClassPicture = "icon-themes/svg/class.svg";

    private readonly TempDir _temp = new();

    private static ThemeId<IconThemeFile> Id(string text)
    {
        return ThemeId<IconThemeFile>.Of(text);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    private string WriteIconTheme(string id, string json)
    {
        return _temp.Write(Path.Combine("icon-themes", id + ".json"), json);
    }

    private AppSettings Settings()
    {
        var settings = _temp.Settings();
        settings.Locale = Locale.En;
        return settings;
    }

    /// <summary>Customizes the icon theme shown as the display options do, naming it in English: switches to the
    /// new icon theme and returns its file, or null when none was written.</summary>
    private static string? CreateCustomIconTheme(AppState appState)
    {
        var themes = appState.IconThemes;
        var custom = appState.UserIconThemes.CreateCustomTheme(appState.IconTheme,
            (parent, theme) => IconThemeDraft.Compose(Strings.En.ThemeCustomName(themes.GetThemeName(parent, Strings.En.Culture)), parent, theme));
        if (custom is null)
        {
            return null;
        }

        appState.IconTheme = custom.Id;
        return custom.Path;
    }

    private static byte[] LucideFont()
    {
        return BuiltInIconThemes.Reference.Fonts[0].Data;
    }

    [Fact]
    public void The_icon_theme_is_lucide_by_default_and_a_chosen_one_is_saved()
    {
        var appState = AppRoot.RestoreAppState(Settings(), TestSession.Shared);
        Assert.Equal("lucide", appState.IconTheme.Text);
        Assert.Equal(BuiltInIconThemes.Reference.Icons.NodeClass, ResolveIconTheme(appState).Icons.NodeClass);

        appState.IconTheme = Id("mine");
        appState.SettingsSaving.SaveIfChanged(appState, 0);

        Assert.Equal("mine", Settings().IconTheme.Text);
    }

    [Fact]
    public void A_missing_icon_theme_loads_as_the_default()
    {
        var path = _temp.Write("settings.json", """{ "IconTheme": null }""");

        Assert.Equal("lucide", AppRoot.ReadSettingsFile(FullFilePath.Of(path)).Settings.IconTheme.Text);
    }

    [Fact]
    public void A_chosen_icon_theme_whose_file_is_gone_shows_lucide_and_stays_chosen()
    {
        var settings = Settings();
        settings.IconTheme = Id("gone");
        var appState = AppRoot.RestoreAppState(settings, TestSession.Shared);

        Assert.Same(appState.IconThemes.GetTheme(Id("lucide")), ResolveIconTheme(appState));
        appState.SettingsSaving.Save(appState);
        Assert.Equal("gone", Settings().IconTheme.Text);
    }

    [Fact]
    public void User_icon_themes_beside_the_settings_are_listed_after_the_built_in_ones_with_their_pictures()
    {
        _temp.Write(ClassPicture, MemoryIconAssets.Picture);
        WriteIconTheme("shapes", """{ "name": "Shapes", "icons": { "node.class": { "svg": "svg/class.svg" } } }""");
        var settings = Settings();
        settings.IconTheme = Id("shapes");
        var appState = AppRoot.RestoreAppState(settings, TestSession.Shared);

        Assert.Equal(["lucide", "shapes"], appState.IconThemes.Ids.Select(static i => i.Text));
        Assert.Equal("Shapes", appState.IconThemes.ListNames(CultureInfo.InvariantCulture).Last());
        Assert.NotNull(ResolveIconTheme(appState).Icons.NodeClass.Svg);
        Assert.Empty(appState.Errors.Entries);
    }

    [Fact]
    public void A_problem_in_an_icon_theme_is_reported_once_under_its_file_until_it_changes()
    {
        WriteIconTheme("escape", """{ "icons": { "node.class": { "svg": "../class.svg" } } }""");
        var appState = AppRoot.RestoreAppState(Settings(), TestSession.Shared);
        var error = Assert.Single(appState.Errors.Entries);

        appState.UserIconThemes.ReloadThemes();

        Assert.Single(appState.Errors.Entries);
        Assert.Equal("Problems in the icon theme escape.json", error.Words().Title);
        Assert.EndsWith("escape.json(1): icons.node.class: ../class.svg is not inside the theme's folder", error.Words().Details);
    }

    [Fact]
    public void The_icon_themes_folder_is_made_with_the_schema_and_a_readme_that_are_no_themes()
    {
        var appState = AppRoot.RestoreAppState(Settings(), TestSession.Shared);

        var folder = appState.UserIconThemes.PrepareFolder();
        appState.UserIconThemes.ReloadThemes();

        Assert.Equal(_temp.PathOf("icon-themes"), folder);
        Assert.NotNull(folder);
        Assert.Equal(BuiltInIconThemes.ReadSchema(), File.ReadAllText(Path.Combine(folder, "icon-theme.schema.json")));
        Assert.Equal(BuiltInIconThemes.ReadGuide(), File.ReadAllText(Path.Combine(folder, "README.md")));
        Assert.Single(appState.IconThemes.Ids);
        Assert.Empty(appState.Errors.Entries);
    }

    [Fact]
    public void A_custom_icon_theme_extends_the_one_shown_lists_every_icon_and_is_switched_to()
    {
        var appState = AppRoot.RestoreAppState(Settings(), TestSession.Shared);

        var path = CreateCustomIconTheme(appState);

        Assert.NotNull(path);
        var text = File.ReadAllText(path);
        Assert.Equal(_temp.PathOf(Path.Combine("icon-themes", "lucide-custom.json")), path);
        Assert.Equal("lucide-custom", appState.IconTheme.Text);
        Assert.Empty(appState.Errors.Entries);
        Assert.Equal("Lucide (custom)", appState.IconThemes.GetThemeName(Id("lucide-custom"), CultureInfo.GetCultureInfo("en")));
        Assert.Equal(BuiltInIconThemes.Reference.Icons.ToolbarBack, ResolveIconTheme(appState).Icons.ToolbarBack);
        Assert.Contains("\"extends\": \"lucide\",", text);
        Assert.Contains("// \"toolbar.back\": { \"font\": \"lucide\", \"glyph\": \"E048\" },", text);
        Assert.Equal(AppIcons.Keys.Count, CountCommentedIcons(text));
    }

    private static int CountCommentedIcons(string text)
    {
        return text.Split('\n').Count(IsCommentedIcon);
    }

    private static bool IsCommentedIcon(string line)
    {
        return line.TrimStart().StartsWith("// \"", StringComparison.Ordinal);
    }

    [Fact]
    public void Each_icon_of_a_custom_icon_theme_can_be_uncommented_alone_and_a_picture_keeps_its_path()
    {
        _temp.Write(ClassPicture, MemoryIconAssets.Picture);
        WriteIconTheme("shapes", """{ "icons": { "node.class": { "svg": "svg/class.svg" } } }""");
        var appState = AppRoot.RestoreAppState(Settings(), TestSession.Shared);
        var catalog = appState.IconThemes;
        var shapes = catalog.GetTheme(Id("shapes"));
        var lines = IconThemeDraft.Compose("Mine", Id("shapes"), shapes).Split('\n');
        var assets = IconAssetFolders.Open(_temp.PathOf("icon-themes"));

        for (var i = 0; i < lines.Length; i++)
        {
            if (!IsCommentedIcon(lines[i]))
            {
                continue;
            }

            var edited = lines.ToArray();
            edited[i] = lines[i].Replace("// ", "");
            var file = IconTheming.ReadTheme(Id("mine"), "mine.json", Encoding.UTF8.GetBytes(string.Join('\n', edited)), assets);
            catalog.ReplaceUserThemes([new UserIconThemesAdapter(new ErrorLog()).ReadThemeFile(Id("shapes"), _temp.PathOf("icon-themes/shapes.json")), file]);
            Assert.True(catalog.Errors.Count == 0, edited[i] + ": " + string.Join("; ", catalog.Errors));
        }
        Assert.Contains(lines, l => l.Trim() == "// \"node.class\": { \"svg\": \"svg/class.svg\" },");
    }

    [Fact]
    public void No_custom_icon_theme_is_made_while_the_settings_live_in_memory()
    {
        var appState = AppRoot.RestoreAppState(new AppSettings { Locale = Locale.En }, TestSession.Shared);

        Assert.Null(CreateCustomIconTheme(appState));
        Assert.Null(appState.UserIconThemes.PrepareFolder());
        Assert.Equal("lucide", appState.IconTheme.Text);
    }

    [Fact]
    public void Saving_a_picture_of_the_chosen_icon_theme_draws_it_at_once()
    {
        _temp.Write(ClassPicture, MemoryIconAssets.Picture);
        WriteIconTheme("live", """{ "icons": { "node.class": { "svg": "svg/class.svg" } } }""");
        var settings = Settings();
        settings.IconTheme = Id("live");
        var d = new AppStateDriver(settings);
        d.AppState.Workflows.StartUntilShutdown(d.AppState.UserIconThemes.WatchAsync);
        var before = ResolveIconTheme(d.AppState).Icons.NodeClass.Svg;

        _temp.Write(ClassPicture, MemoryIconAssets.Picture.Replace("currentColor", "#FF0000"));
        d.RunUntil(() => ResolveIconTheme(d.AppState).Icons.NodeClass.Svg != before);
        d.AppState.Workflows.CancelAll();

        var after = ResolveIconTheme(d.AppState).Icons.NodeClass.Svg;
        Assert.NotNull(after);
        Assert.False(after.UsesCurrentColor);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void Saving_a_font_of_the_chosen_icon_theme_registers_it_again_with_the_screen()
    {
        var font = _temp.Write("icon-themes/fonts/mine.ttf", LucideFont());
        WriteIconTheme("live", """{ "fonts": { "m": "fonts/mine.ttf" }, "icons": { "node.class": { "font": "m", "glyph": "E106" } } }""");
        var settings = Settings();
        settings.IconTheme = Id("live");
        var app = new AppDriver(settings: settings);
        app.AppState.Workflows.StartUntilShutdown(app.AppState.UserIconThemes.WatchAsync);
        app.Run(2);
        var family = "icon-font:" + Path.GetFullPath(font);
        Assert.Contains(app.Frames.RegisteredFonts, f => f.Family == family);
        var registered = app.Frames.RegisteredFonts.Count;

        File.WriteAllBytes(font, LucideFont().Append((byte)0).ToArray());
        app.Frames.WaitUntil(() => app.Frames.RegisteredFonts.Count > registered, Patience);
        app.AppState.Workflows.CancelAll();

        Assert.Equal(LucideFont().Length + 1, app.Frames.RegisteredFonts.Last(f => f.Family == family).Data.Length);
        Assert.Equal(family, app.Ui.Icons.NodeClass.Family);
    }

    [Fact]
    public void Leaving_an_icon_theme_lets_go_of_its_fonts_but_never_of_lucides()
    {
        var font = _temp.Write("icon-themes/fonts/mine.ttf", LucideFont());
        WriteIconTheme("mine", """{ "fonts": { "m": "fonts/mine.ttf" }, "icons": { "node.class": { "font": "m", "glyph": "E106" } } }""");
        var settings = Settings();
        settings.IconTheme = Id("mine");
        var app = new AppDriver(settings: settings);
        app.Run(2);
        Assert.Empty(app.Frames.UnregisteredFonts);

        app.AppState.IconTheme = Id("lucide");
        app.Run(2);

        Assert.Equal(["icon-font:" + Path.GetFullPath(font)], app.Frames.UnregisteredFonts);
        Assert.Equal("lucide", app.Ui.Icons.NodeClass.Family);
    }

    [Fact]
    public void The_icon_theme_is_picked_from_the_display_options_with_its_buttons()
    {
        _temp.Write(ClassPicture, MemoryIconAssets.Picture);
        WriteIconTheme("shapes", """{ "name": "Shapes", "icons": { "node.class": { "svg": "svg/class.svg" } } }""");
        var app = new AppDriver(width: 1600f, settings: Settings());
        OpenPanel(app.Ui, DockView.DisplayOptions);
        app.Run();

        app.Frames.ClickOn(Selector.AtPath("dock/display-options/icon-theme")).Frame();
        app.Frames.ClickOn(Selector.WithText("Shapes")).Frame(2);

        Assert.Equal("shapes", app.AppState.IconTheme.Text);
        Assert.NotNull(app.Ui.Icons.NodeClass.Svg);
        Assert.True(app.Frames.Drew(Text(Icons.OptionsCustomizeIconTheme)));
    }

    [Fact]
    public void Customizing_the_icon_theme_switches_to_a_new_one_and_opens_its_file()
    {
        var app = new AppDriver(width: 1600f, settings: Settings());
        OpenPanel(app.Ui, DockView.DisplayOptions);
        app.Run();

        app.Frames.ClickOn(Selector.AtPath("dock/display-options/customize-icon-theme")).Frame();

        Assert.Equal("lucide-custom", app.AppState.IconTheme.Text);
        Assert.Equal([_temp.PathOf(Path.Combine("icon-themes", "lucide-custom.json"))], app.Frames.OpenedFiles);
    }

    [Fact]
    public void The_icon_theme_buttons_hide_while_the_settings_live_in_memory()
    {
        var app = new AppDriver(width: 1600f);
        OpenPanel(app.Ui, DockView.DisplayOptions);
        app.Run();

        Assert.True(app.Frames.Drew("Icon theme"));
        Assert.False(app.Frames.Drew(Text(Icons.OptionsCustomizeIconTheme)));
    }
}
