using ILSight.Core;
using ILSight.BuiltInArea;

namespace ILSight.UserArea;

/// <summary>The seam around the user's icon themes folder, for <see cref="UserThemes{TFile, TContents, TTheme}"/>
/// of icon themes: the theme files at the top of the folder, and the fonts and pictures they name anywhere in it,
/// read with the file that names them.</summary>
public sealed class UserIconThemesAdapter(ErrorLog errors) : UserThemesAdapter<IconThemeFile, IconThemeContents, IconTheme>(errors, CreateWatch())
{
    protected override string SchemaFileName => "icon-theme.schema.json";

    protected override string ReadSchema()
    {
        return BuiltInIconThemes.ReadSchema();
    }

    protected override string ReadGuide()
    {
        return BuiltInIconThemes.ReadGuide();
    }

    protected override IconThemeFile ReadTheme(ThemeId<IconThemeFile> id, string path, byte[] json)
    {
        return IconTheming.ReadTheme(id, path, json, OpenAssets(path));
    }

    protected override IconThemeFile CreateUnreadableTheme(ThemeId<IconThemeFile> id, string path, string reason)
    {
        return IconTheming.CreateUnreadableTheme(id, path, OpenAssets(path), reason);
    }

    /// <summary>The fonts and pictures inside the folder the file at <paramref name="path"/> is in.</summary>
    private static IconAssets OpenAssets(string path)
    {
        return IconAssetFolders.Open(FullFilePath.Of(Path.GetFullPath(path)).Folder);
    }

    private static ThemeFolderWatch CreateWatch()
    {
        return new ThemeFolderWatch { Filters = ["*" + IconThemeFile.Extension, "*.ttf", "*.otf", "*.svg"], IncludesSubfolders = true };
    }
}
