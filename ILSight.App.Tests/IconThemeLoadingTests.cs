using System.Globalization;
using System.Text;
using ZGF.Gui.Immediate;

namespace ILSight.App.Tests;

public sealed class IconThemeLoadingTests
{
    private const int Box = 0xE061;
    private const int List = 0xE106;
    private const int TruncatedLength = 2000;

    private static readonly byte[] FontBytes = BuiltInIconThemes.Reference.Fonts[0].Data;

    private static string Glyph(int codepoint)
    {
        return char.ConvertFromUtf32(codepoint);
    }

    private static ThemeId<IconThemeFile> Id(string text)
    {
        return ThemeId<IconThemeFile>.Of(text);
    }

    private static IconThemeFile User(string id, string json, MemoryIconAssets? assets = null)
    {
        return IconTheming.ReadTheme(Id(id), id + ".json", Encoding.UTF8.GetBytes(json), assets ?? new MemoryIconAssets());
    }

    private static ThemeCatalog<IconThemeFile, IconThemeContents, IconTheme> BuildCatalog(params IconThemeFile[] users)
    {
        var catalog = IconTheming.CreateCatalog(BuiltInIconThemes.Files);
        catalog.ReplaceUserThemes(users);
        return catalog;
    }

    private static IconGlyph Reference(string key)
    {
        return BuiltInIconThemes.Reference.Set.Find(key);
    }

    [Fact]
    public void A_partial_theme_draws_its_own_keys_and_the_reference_themes_for_the_rest()
    {
        var assets = new MemoryIconAssets().With("mine.ttf", FontBytes);
        var catalog = BuildCatalog(User("mine", """
            { "fonts": { "m": "mine.ttf" },
              "icons": { "node.class": { "font": "m", "glyph": "E106" } } }
            """, assets));

        var mine = catalog.GetTheme(Id("mine"));

        Assert.Empty(catalog.Errors);
        Assert.Equal(IconGlyph.FromFont(Glyph(List), "memory:mine.ttf"), mine.Icons.NodeClass);
        Assert.Equal(Reference("node.method"), mine.Icons.NodeMethod);
        Assert.Equal(["lucide", "memory:mine.ttf"], mine.Fonts.Select(f => f.Family));
        Assert.Equal("mine", catalog.Ids[^1].Text);
    }

    [Fact]
    public void A_theme_draws_in_the_reference_themes_font_by_its_id()
    {
        var catalog = BuildCatalog(User("swap", """{ "icons": { "node.class": { "font": "lucide", "glyph": "E106" } } }"""));

        var swap = catalog.GetTheme(Id("swap"));

        Assert.Empty(catalog.Errors);
        Assert.Equal(Reference("node.metadataDirectory"), swap.Icons.NodeClass);
    }

    [Fact]
    public void A_child_takes_the_rest_from_the_theme_it_extends_and_redeclaring_a_font_draws_the_parents_icons_in_it()
    {
        var assets = new MemoryIconAssets()
            .With("a.ttf", FontBytes)
            .With("b.ttf", FontBytes);
        var catalog = BuildCatalog(
            User("parent", """
                { "fonts": { "f": "a.ttf" },
                  "icons": { "node.class": { "font": "f", "glyph": "E106" }, "node.method": { "font": "f", "glyph": "E061" } } }
                """, assets),
            User("child", """
                { "extends": "parent", "fonts": { "f": "b.ttf" },
                  "icons": { "node.method": { "font": "lucide", "glyph": "E061" } } }
                """, assets));

        var child = catalog.GetTheme(Id("child"));

        Assert.Empty(catalog.Errors);
        Assert.Equal(IconGlyph.FromFont(Glyph(List), "memory:b.ttf"), child.Icons.NodeClass);
        Assert.Equal(Reference("node.class"), child.Icons.NodeMethod);
        Assert.Equal(IconGlyph.FromFont(Glyph(List), "memory:a.ttf"), catalog.GetTheme(Id("parent")).Icons.NodeClass);
    }

    [Fact]
    public void An_svg_icon_is_a_picture_whose_value_is_its_path()
    {
        var assets = new MemoryIconAssets().With("svg/class.svg", MemoryIconAssets.Picture);
        var catalog = BuildCatalog(User("pictures", """{ "icons": { "node.class": { "svg": "svg/class.svg" } } }""", assets));

        var theme = catalog.GetTheme(Id("pictures"));

        Assert.Empty(catalog.Errors);
        Assert.NotNull(theme.Icons.NodeClass.Svg);
        Assert.True(theme.Icons.NodeClass.Svg!.UsesCurrentColor);
        Assert.Equal("svg/class.svg", Assert.IsType<IconValue.Picture>(theme.Values["node.class"]).SvgPath);
        Assert.Equal("lucide", Assert.IsType<IconValue.Glyph>(theme.Values["node.method"]).Font);
    }

    public static TheoryData<string, ThemeProblem, string, string> BadInput => new TheoryData<string, ThemeProblem, string, string>()
    {
        { """{ "icons": { "node.klass": { "font": "lucide", "glyph": "E061" } } }""", ThemeProblem.UnknownKey, "icons.node.klass", "" },
        { """{ "icons": { "node.class": { "font": "lucide", "glyph": "XYZ" } } }""", ThemeProblem.NotACodepoint, "icons.node.class", "XYZ" },
        { """{ "icons": { "node.class": { "font": "lucide", "glyph": "D800" } } }""", ThemeProblem.NotACodepoint, "icons.node.class", "D800" },
        { """{ "icons": { "node.class": { "font": "lucide", "glyph": "1234567" } } }""", ThemeProblem.NotACodepoint, "icons.node.class", "1234567" },
        { """{ "icons": { "node.class": { "font": "lucide" } } }""", ThemeProblem.InvalidIcon, "icons.node.class", "" },
        { """{ "icons": { "node.class": { "svg": "a.svg", "font": "lucide", "glyph": "E061" } } }""", ThemeProblem.InvalidIcon, "icons.node.class", "" },
        { """{ "icons": { "node.class": "E061" } }""", ThemeProblem.NotAnObject, "icons.node.class", "" },
        { """{ "icons": { "node.class": { "svg": "missing.svg" } } }""", ThemeProblem.AssetUnreadable, "icons.node.class", "missing.svg" },
        { """{ "icons": { "node.class": { "svg": "bad.svg" } } }""", ThemeProblem.InvalidSvg, "icons.node.class", "bad.svg: " },
        { """{ "fonts": { "f": "missing.ttf" } }""", ThemeProblem.AssetUnreadable, "fonts.f", "missing.ttf" },
        { """{ "fonts": { "f": "bad.svg" } }""", ThemeProblem.NotAFont, "fonts.f", "bad.svg" },
        { """{ "fonts": [] }""", ThemeProblem.NotAnObject, "fonts", "" },
        { """{ "glyphs": {} }""", ThemeProblem.UnknownKey, "glyphs", "" },
    };

    [Theory]
    [MemberData(nameof(BadInput))]
    public void Bad_input_is_reported_with_the_key_and_left_out(string json, ThemeProblem problem, string subject, string detail)
    {
        var assets = new MemoryIconAssets().With("bad.svg", "<svg");
        var file = User("bad", json, assets);

        var error = Assert.Single(file.Errors);

        Assert.Equal(problem, error.Problem);
        Assert.Equal(subject, error.Subject);
        Assert.StartsWith(detail, error.Detail);
        Assert.Equal(("bad.json", 1), (error.File, error.Line));
        Assert.Empty(Assert.IsType<IconThemeContents>(file.Contents).Icons);
    }

    [Fact]
    public void An_icon_the_app_no_longer_draws_is_passed_over_without_an_error()
    {
        var file = User("older", """{ "icons": { "file.exit": { "font": "lucide", "glyph": "E10E" }, "node.class": { "font": "lucide", "glyph": "E061" } } }""");

        Assert.Empty(file.Errors);
        Assert.Equal(["node.class"], Assert.IsType<IconThemeContents>(file.Contents).Icons.Keys);
    }

    [Fact]
    public void An_unknown_field_of_an_icon_is_reported_and_the_icon_kept()
    {
        var file = User("sized", """{ "icons": { "node.class": { "font": "lucide", "glyph": "E061", "size": 3 } } }""");

        var error = Assert.Single(file.Errors);

        Assert.Equal((ThemeProblem.UnknownKey, "icons.node.class.size"), (error.Problem, error.Subject));
        Assert.Equal(0xE061, Assert.IsType<IconValue.Glyph>(Assert.IsType<IconThemeContents>(file.Contents).Icons["node.class"]).Codepoint);
    }

    [Fact]
    public void An_icon_in_a_font_no_theme_declares_is_reported_where_it_is_written_and_drawn_as_beneath_it()
    {
        var catalog = BuildCatalog(User("orphan", """
            {
              "icons": { "node.class": { "font": "nope", "glyph": "E106" } }
            }
            """));

        var error = Assert.Single(catalog.Errors);

        Assert.Equal((ThemeProblem.UnknownFont, 2, "icons.node.class", "nope"), (error.Problem, error.Line, error.Subject, error.Detail));
        Assert.Equal(Reference("node.class"), catalog.GetTheme(Id("orphan")).Icons.NodeClass);
    }

    [Fact]
    public void An_icon_in_a_font_that_could_not_be_read_is_drawn_as_beneath_it_and_reported_once()
    {
        var catalog = BuildCatalog(User("broken", """
            { "fonts": { "f": "missing.ttf" },
              "icons": { "node.class": { "font": "f", "glyph": "E106" } } }
            """));

        var error = Assert.Single(catalog.Errors);

        Assert.Equal(ThemeProblem.AssetUnreadable, error.Problem);
        Assert.Equal(Reference("node.class"), catalog.GetTheme(Id("broken")).Icons.NodeClass);
    }

    [Fact]
    public void A_font_cut_short_is_reported_as_no_font_and_its_icons_are_drawn_as_beneath_them()
    {
        var assets = new MemoryIconAssets().With("cut.ttf", FontBytes.AsSpan(0, TruncatedLength).ToArray());
        var catalog = BuildCatalog(User("cut", """
            { "fonts": { "f": "cut.ttf" },
              "icons": { "node.class": { "font": "f", "glyph": "E106" } } }
            """, assets));

        var error = Assert.Single(catalog.Errors);

        Assert.Equal((ThemeProblem.NotAFont, 1, "fonts.f", "cut.ttf"), (error.Problem, error.Line, error.Subject, error.Detail));
        Assert.Equal(Reference("node.class"), catalog.GetTheme(Id("cut")).Icons.NodeClass);
        Assert.Equal(["lucide"], catalog.GetTheme(Id("cut")).Fonts.Select(f => f.Family));
    }

    [Fact]
    public void An_icon_whose_font_has_no_glyph_for_it_is_reported_and_drawn_as_beneath_it()
    {
        var catalog = BuildCatalog(User("tofu", """
            {
              "icons": { "node.class": { "font": "lucide", "glyph": "41" },
                         "node.method": { "font": "lucide", "glyph": "E061" } }
            }
            """));

        var error = Assert.Single(catalog.Errors);

        Assert.Equal((ThemeProblem.MissingGlyph, 2, "icons.node.class", "0041"), (error.Problem, error.Line, error.Subject, error.Detail));
        Assert.Equal(Reference("node.class"), catalog.GetTheme(Id("tofu")).Icons.NodeClass);
        Assert.Equal(IconGlyph.FromFont(Glyph(Box), "lucide"), catalog.GetTheme(Id("tofu")).Icons.NodeMethod);
    }

    [Fact]
    public void Comments_and_trailing_commas_are_allowed_and_codepoints_are_hex_in_any_case()
    {
        var file = User("commented", """
            {
              // the class icon only
              "icons": {
                "node.class": { "font": "lucide", "glyph": "e061", },
              },
            }
            """);

        Assert.Empty(file.Errors);
        Assert.Equal(0xE061, Assert.IsType<IconValue.Glyph>(Assert.IsType<IconThemeContents>(file.Contents).Icons["node.class"]).Codepoint);
    }

    [Theory]
    [InlineData("lucide")]
    [InlineData("Lucide")]
    public void A_user_icon_theme_with_the_reference_ones_id_is_reported_and_left_out(string id)
    {
        var catalog = BuildCatalog(User(id, """{ "icons": {} }"""));

        var error = Assert.Single(catalog.Errors);

        Assert.Equal((ThemeProblem.IdTaken, id), (error.Problem, error.Subject));
        Assert.Single(catalog.Ids);
    }

    [Fact]
    public void Icon_themes_that_extend_each_other_in_a_circle_are_reported_and_fall_back_to_the_reference()
    {
        var catalog = BuildCatalog(
            User("a", """{ "extends": "b" }"""),
            User("b", """{ "extends": "a" }"""));

        var cycles = catalog.Errors
            .Where(e => e.Problem == ThemeProblem.ExtendsCycle)
            .Select(e => e.Detail);

        Assert.Equal(["a → b → a", "b → a → b"], cycles);
        Assert.Same(catalog.GetTheme(Id("lucide")), catalog.GetTheme(Id("a")));
        Assert.Equal("lucide", catalog.GetShownId(Id("b")).Text);
    }

    [Fact]
    public void An_icon_theme_extending_no_known_theme_is_reported_and_falls_back_to_the_reference()
    {
        var catalog = BuildCatalog(User("orphan", """{ "extends": "nope" }"""));

        var error = Assert.Single(catalog.Errors);

        Assert.Equal((ThemeProblem.UnknownParent, "nope"), (error.Problem, error.Subject));
        Assert.Same(catalog.GetTheme(Id("lucide")), catalog.GetTheme(Id("orphan")));
    }

    [Fact]
    public void An_unreadable_icon_theme_falls_back_to_the_theme_it_extends()
    {
        var catalog = BuildCatalog(
            User("base", """{ "icons": { "node.class": { "font": "lucide", "glyph": "E106" } } }"""),
            User("broken", """
                { "extends": "base",
                  "icons": }
                """));

        var error = Assert.Single(catalog.Errors);

        Assert.Equal((ThemeProblem.InvalidJson, 2), (error.Problem, error.Line));
        Assert.Same(catalog.GetTheme(Id("base")), catalog.GetTheme(Id("broken")));
    }

    [Fact]
    public void A_name_per_language_falls_back_to_english()
    {
        var catalog = BuildCatalog(User("named", """{ "name": { "en": "Shapes", "de": "Formen" } }"""));

        Assert.Equal(["Lucide", "Formen"], catalog.ListNames(CultureInfo.GetCultureInfo("de")));
        Assert.Equal("Shapes", catalog.GetThemeName(Id("named"), CultureInfo.GetCultureInfo("fr")));
    }
}
