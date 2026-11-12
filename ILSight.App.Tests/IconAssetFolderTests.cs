using ILSight.UserArea;

namespace ILSight.App.Tests;

public sealed class IconAssetFolderTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    /// <summary>Paths that lead out of the folder; a drive's path only on Windows, since elsewhere
    /// <c>C:</c> is the name of a folder inside it.</summary>
    public static TheoryData<string> PathsOutside()
    {
        var paths = new TheoryData<string> { "../outside.svg", "svg/../../outside.svg", "/outside.svg" };
        if (OperatingSystem.IsWindows())
        {
            paths.Add("C:/outside.svg");
        }

        return paths;
    }

    [Theory]
    [MemberData(nameof(PathsOutside))]
    public void A_path_that_leaves_the_folder_is_refused(string path)
    {
        _temp.Write("outside.svg", MemoryIconAssets.Picture);
        var folder = _temp.PathOf("icon-themes");

        var asset = IconAssetFolders.Read(folder, path);

        Assert.Null(asset.Bytes);
        Assert.Equal((ThemeProblem.AssetOutsideFolder, path), (asset.Problem, asset.Detail));
    }

    [Fact]
    public void A_path_no_path_may_be_is_unreadable_rather_than_thrown()
    {
        var folder = _temp.PathOf("icon-themes");
        var path = "a" + (char)0 + ".svg";

        var asset = IconAssetFolders.Read(folder, path);

        Assert.Null(asset.Bytes);
        Assert.Equal(ThemeProblem.AssetUnreadable, asset.Problem);
        Assert.StartsWith(path + ": ", asset.Detail);
    }

    [Fact]
    public void An_icon_theme_naming_a_path_no_path_may_be_is_reported_and_the_rest_still_loads()
    {
        var escapedNul = (char)92 + "u0000";
        _temp.Write("icon-themes/bad.json", "{ \"icons\": { \"node.class\": { \"svg\": \"a" + escapedNul + ".svg\" } } }");
        _temp.Write("icon-themes/good.json", "{}");
        var settings = _temp.Settings();
        settings.Locale = Locale.En;

        var appState = AppRoot.RestoreAppState(settings, TestSession.Shared);

        var error = Assert.Single(appState.Errors.Entries);
        Assert.Equal("Problems in the icon theme bad.json", error.Words().Title);
        Assert.Equal(["lucide", "bad", "good"], appState.IconThemes.Ids.Select(static i => i.Text));
    }

    [Fact]
    public void A_path_inside_the_folder_is_read_and_a_font_is_drawn_as_a_family_of_its_own_file()
    {
        _temp.Write("icon-themes/svg/a.svg", MemoryIconAssets.Picture);
        _temp.Write("icon-themes/fonts/f.ttf", "not read as a font here");
        var folder = _temp.PathOf("icon-themes");

        var asset = IconAssetFolders.Read(folder, "svg/../svg/a.svg");

        Assert.NotNull(asset.Bytes);
        Assert.Equal("icon-font:" + _temp.PathOf(Path.Combine("icon-themes", "fonts", "f.ttf")), IconAssetFolders.Read(folder, "fonts/f.ttf").Family);
    }
}
