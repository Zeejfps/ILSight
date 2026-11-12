namespace ILSight.App.Tests;

public sealed class BuiltInIconThemeTests
{
    [Fact]
    public void The_reference_icon_theme_loads_without_errors_and_draws_every_key_in_its_font()
    {
        var catalog = IconTheming.CreateCatalog(BuiltInIconThemes.Files);
        var reference = catalog.GetTheme(IconTheming.ReferenceId);

        Assert.Empty(BuiltInIconThemes.Errors);
        Assert.Empty(catalog.Errors);
        Assert.Equal(["lucide"], catalog.Ids.Select(static i => i.Text));
        Assert.All(AppIcons.Keys, k => Assert.True(reference.Set.Contains(k.Key), k.Key));
        Assert.All(AppIcons.Keys, k => Assert.Equal("lucide", reference.Set.Find(k.Key).Family));
        Assert.Equal(["lucide"], reference.Fonts.Select(f => f.Family));
    }

    [Fact]
    public void Every_key_is_dotted_by_area_and_described()
    {
        Assert.All(AppIcons.Keys, k => Assert.Matches("^[a-z]+\\.[a-z][A-Za-z]*$", k.Key));
        Assert.All(AppIcons.Keys, k => Assert.NotEmpty(k.Description));
        Assert.Equal("A class.", AppIcons.Keys.Single(k => k.Key == "node.class").Description);
    }

    [Fact]
    public void Every_node_kind_draws_its_own_node_key()
    {
        var icons = BuiltInIconThemes.Reference.Icons;

        foreach (var kind in Enum.GetValues<NodeIcon>())
        {
            var key = "node." + char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString().Substring(1);
            Assert.Equal(BuiltInIconThemes.Reference.Set.Find(key), NodeIcons.Glyph(kind, icons));
        }
    }
}
