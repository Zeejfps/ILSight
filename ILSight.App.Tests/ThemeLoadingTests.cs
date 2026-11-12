using System.Globalization;
using System.Text;
using ZGF.Gui.Immediate;

namespace ILSight.App.Tests;

public sealed class ThemeLoadingTests
{
    private const string BaseRoles = """
        "background": "#101010", "surface": "#141414", "control": "#202020", "field": "#0C0C0C", "border": "#404040",
        "text": "#F0F0F0", "textMuted": "#B0B0B0", "accent": "#3070F0", "onAccent": "#FFFFFF"
        """;

    private static ThemeId<ThemeFile> Id(string text)
    {
        return ThemeId<ThemeFile>.Of(text);
    }

    private static ThemeFile User(string id, string json)
    {
        return Theming.ReadTheme(Id(id), id + ".json", Encoding.UTF8.GetBytes(json));
    }

    private static ThemeCatalog<ThemeFile, ThemeContents, AppTheme> BuildCatalog(params ThemeFile[] users)
    {
        var catalog = Theming.CreateCatalog(BuiltInThemes.Files);
        catalog.ReplaceUserThemes(users);
        return catalog;
    }

    [Fact]
    public void A_partial_theme_takes_the_rest_from_the_theme_it_extends()
    {
        var catalog = BuildCatalog(User("my-dracula", """
            { "extends": "dracula", "ui": { "accent": "#FF0000" }, "code": { "keyword": "#00FF00" } }
            """));
        var dracula = catalog.GetTheme(Id("dracula"));

        var mine = catalog.GetTheme(Id("my-dracula"));

        Assert.Empty(catalog.Errors);
        Assert.Equal(0xFFFF0000u, mine.Ui.Accent);
        Assert.Equal(dracula.Ui.Background, mine.Ui.Background);
        Assert.Equal(0xFF00FF00u, mine.Code.Keyword);
        Assert.Equal(dracula.Code.String, mine.Code.String);
        Assert.Equal(dracula.Icons.Type, mine.Icons.Type);
        Assert.Equal("my-dracula", catalog.Ids[^1].Text);
    }

    [Fact]
    public void A_child_that_changes_a_base_role_gets_matching_derived_colors()
    {
        var catalog = BuildCatalog(User("dim", """{ "extends": "github-dark", "ui": { "control": "#505050" } }"""));

        var dim = catalog.GetTheme(Id("dim")).Ui;

        Assert.Equal(UiColor.Mix(0xFF505050, dim.Text, 0.06f), dim.ControlHovered);
        Assert.Equal(UiColor.Mix(0xFF505050, dim.Text, 0.12f), dim.ControlPressed);
    }

    [Fact]
    public void Palette_names_take_the_palette_merged_over_the_extended_themes()
    {
        var catalog = BuildCatalog(
            User("base", $$"""{ "type": "dark", "palette": { "bg": "#111111" }, "ui": { {{BaseRoles}}, "editorBackground": "bg" } }"""),
            User("child", """{ "extends": "base", "palette": { "bg": "#222222" } }"""));

        Assert.Empty(catalog.Errors);
        Assert.Equal(0xFF111111u, catalog.GetTheme(Id("base")).Ui.EditorBackground);
        Assert.Equal(0xFF222222u, catalog.GetTheme(Id("child")).Ui.EditorBackground);
    }

    [Fact]
    public void A_theme_that_extends_none_takes_its_code_and_icon_colors_from_the_built_in_one_of_its_type()
    {
        var catalog = BuildCatalog(User("paper", $$"""{ "type": "light", "ui": { {{BaseRoles}} } }"""));
        var light = catalog.GetTheme(Id("light"));

        var paper = catalog.GetTheme(Id("paper"));

        Assert.Empty(catalog.Errors);
        Assert.Equal(0xFF101010u, paper.Ui.Background);
        Assert.Equal(UiColor.Mix(0xFF202020, 0xFFF0F0F0, 0.06f), paper.Ui.ControlHovered);
        Assert.Equal(light.Code.Keyword, paper.Code.Keyword);
        Assert.Equal(light.Icons.Method, paper.Icons.Method);
    }

    [Fact]
    public void A_theme_missing_base_roles_is_reported_and_falls_back_to_the_built_in_one_of_its_type()
    {
        var catalog = BuildCatalog(User("sparse", """{ "type": "light", "ui": { "background": "#FFFFFF" } }"""));

        var error = Assert.Single(catalog.Errors);

        Assert.Equal(ThemeProblem.MissingColors, error.Problem);
        Assert.Equal("ui.surface, ui.control, ui.field, ui.border, ui.text, ui.textMuted, ui.accent, ui.onAccent", error.Detail);
        Assert.Same(catalog.GetTheme(Id("light")), catalog.GetTheme(Id("sparse")));
    }

    [Fact]
    public void A_theme_that_cannot_be_read_falls_back_to_the_theme_it_extends()
    {
        var catalog = BuildCatalog(User("broken", """
            { "extends": "monokai",
              "ui": { "accent": } }
            """));

        var error = Assert.Single(catalog.Errors);

        Assert.Equal(ThemeProblem.InvalidJson, error.Problem);
        Assert.Equal(2, error.Line);
        Assert.Same(catalog.GetTheme(Id("monokai")), catalog.GetTheme(Id("broken")));
    }

    public static TheoryData<string, ThemeProblem, string> BadInput => new TheoryData<string, ThemeProblem, string>()
    {
        { """[]""", ThemeProblem.RootNotAnObject, "" },
        { """{ "colour": "#FFFFFF" }""", ThemeProblem.UnknownKey, "colour" },
        { """{ "ui": { "backgroundd": "#FFFFFF" } }""", ThemeProblem.UnknownKey, "ui.backgroundd" },
        { """{ "ui": { "background": "#FFFFF" } }""", ThemeProblem.NotAColor, "ui.background" },
        { """{ "palette": { "bg": "bg2" } }""", ThemeProblem.NotAColor, "palette.bg" },
        { """{ "ui": { "background": "#00000000" } }""", ThemeProblem.TransparentBlack, "ui.background" },
        { """{ "ui": { "background": 3 } }""", ThemeProblem.NotAString, "ui.background" },
        { """{ "ui": [] }""", ThemeProblem.NotAnObject, "ui" },
        { """{ "type": "dim" }""", ThemeProblem.UnknownType, "dim" },
        { """{ "name": 3 }""", ThemeProblem.InvalidName, "" },
        { """{ "name": { "de": 3 } }""", ThemeProblem.NotAString, "name.de" },
    };

    [Theory]
    [MemberData(nameof(BadInput))]
    public void Bad_input_is_reported_with_the_key_and_left_out(string json, ThemeProblem problem, string subject)
    {
        var file = User("bad", json);

        var error = Assert.Single(file.Errors);

        Assert.Equal(problem, error.Problem);
        Assert.Equal(subject, error.Subject);
        Assert.Equal("bad.json", error.File);
        Assert.Equal(1, error.Line);
        if (file.Contents != null)
        {
            Assert.Empty(file.Contents.Ui);
        }
    }

    [Fact]
    public void Comments_and_trailing_commas_are_allowed_and_errors_tell_their_line()
    {
        var file = User("commented", """
            {
              // builds on the default
              "extends": "dark",
              "ui": {
                "accent": "#12345",
                "text": "#EEEEEE",
              },
            }
            """);

        var error = Assert.Single(file.Errors);

        var contents = Assert.IsType<ThemeContents>(file.Contents);
        Assert.Equal((ThemeProblem.NotAColor, 5, "#12345"), (error.Problem, error.Line, error.Detail));
        Assert.Equal(["text"], contents.Ui.Keys);
    }

    [Fact]
    public void An_unknown_palette_name_is_reported_where_it_is_written_and_the_color_is_left_to_the_parent()
    {
        var catalog = BuildCatalog(User("named", """
            { "extends": "dark",
              "ui": { "accent": "purple" } }
            """));

        var error = Assert.Single(catalog.Errors);

        Assert.Equal((ThemeProblem.UnknownPaletteName, 2, "ui.accent", "purple"), (error.Problem, error.Line, error.Subject, error.Detail));
        Assert.Equal(catalog.GetTheme(Id("dark")).Ui.Accent, catalog.GetTheme(Id("named")).Ui.Accent);
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("Dark")]
    public void A_user_theme_with_a_built_in_ones_id_is_reported_and_left_out(string id)
    {
        var catalog = BuildCatalog(User(id, """{ "extends": "light" }"""));

        var error = Assert.Single(catalog.Errors);

        Assert.Equal((ThemeProblem.IdTaken, id), (error.Problem, error.Subject));
        Assert.Equal(BuiltInThemes.Files.Count, catalog.Ids.Count);
        Assert.True(catalog.GetTheme(Id("dark")).Ui.IsDark);
    }

    [Fact]
    public void Themes_that_extend_each_other_in_a_circle_are_reported_and_fall_back()
    {
        var catalog = BuildCatalog(
            User("a", """{ "type": "dark", "extends": "b" }"""),
            User("b", """{ "type": "light", "extends": "a" }"""));

        var cycles = catalog.Errors
            .Where(e => e.Problem == ThemeProblem.ExtendsCycle)
            .Select(e => e.Detail);

        Assert.Equal(["a → b → a", "b → a → b"], cycles);
        Assert.Same(catalog.GetTheme(Id("light")), catalog.GetTheme(Id("a")));
        Assert.Same(catalog.GetTheme(Id("light")), catalog.GetTheme(Id("b")));
    }

    [Fact]
    public void A_theme_extending_no_known_theme_is_reported_and_falls_back_by_its_type()
    {
        var catalog = BuildCatalog(User("orphan", """
            { "type": "light",
              "extends": "nope" }
            """));

        var error = Assert.Single(catalog.Errors);

        Assert.Equal((ThemeProblem.UnknownParent, 2, "nope"), (error.Problem, error.Line, error.Subject));
        Assert.Same(catalog.GetTheme(Id("light")), catalog.GetTheme(Id("orphan")));
    }

    [Fact]
    public void A_parent_that_names_no_file_is_reported()
    {
        var catalog = BuildCatalog(User("escape", """
            { "type": "light",
              "extends": "../light" }
            """));

        Assert.Contains(catalog.Errors, e => (e.Problem, e.Line, e.Subject) == (ThemeProblem.UnknownParent, 2, "../light"));
        Assert.Same(catalog.GetTheme(Id("light")), catalog.GetTheme(Id("escape")));
    }

    [Fact]
    public void An_id_is_found_in_any_case()
    {
        var catalog = BuildCatalog();

        Assert.Same(catalog.GetTheme(Id("dracula")), catalog.GetTheme(Id("Dracula")));
        Assert.Equal(catalog.FindThemeIndex(Id("dracula")), catalog.FindThemeIndex(Id("DRACULA")));
    }

    [Fact]
    public void A_theme_with_neither_a_type_nor_a_parent_is_reported_and_falls_back_to_dark()
    {
        var catalog = BuildCatalog(User("typeless", $$"""{ "ui": { {{BaseRoles}} } }"""));

        var error = Assert.Single(catalog.Errors);

        Assert.Equal(ThemeProblem.NoType, error.Problem);
        Assert.Same(catalog.GetTheme(Id("dark")), catalog.GetTheme(Id("typeless")));
    }

    [Fact]
    public void An_id_no_theme_has_shows_the_dark_theme()
    {
        var catalog = BuildCatalog();

        Assert.Same(catalog.GetTheme(Id("dark")), catalog.GetTheme(Id("gone")));
        Assert.Null(catalog.FindThemeIndex(Id("gone")));
    }

    [Fact]
    public void A_language_named_twice_in_any_case_is_reported_and_its_first_name_kept()
    {
        var file = User("twice", """
            { "extends": "dark",
              "name": { "en": "Night",
                        "EN": "Nacht" } }
            """);
        var catalog = BuildCatalog(file);

        var error = Assert.Single(catalog.Errors);

        Assert.Equal((ThemeProblem.DuplicateLanguage, 3, "name.EN"), (error.Problem, error.Line, error.Subject));
        Assert.Equal("Night", ThemeNames.GetName(file.Name, CultureInfo.GetCultureInfo("en")));
    }

    [Fact]
    public void A_name_per_language_falls_back_to_english()
    {
        var catalog = BuildCatalog(User("named", """{ "extends": "dark", "name": { "en": "Night", "de": "Nacht" } }"""));

        Assert.Equal("Nacht", catalog.ListNames(CultureInfo.GetCultureInfo("de")).Last());
        Assert.Equal("Night", catalog.ListNames(CultureInfo.GetCultureInfo("fr")).Last());
    }
}
