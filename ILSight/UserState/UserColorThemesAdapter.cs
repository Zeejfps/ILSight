using ILSight.BuiltInAssets;

namespace ILSight.UserState;

/// <summary>The seam around the user's color themes folder, for <see cref="UserThemes{TFile, TContents, TTheme}"/>
/// of color themes: the theme files at the top of the folder.</summary>
public sealed class UserColorThemesAdapter(ErrorLog errors) : UserThemesAdapter<ThemeFile, ThemeContents, AppTheme>(errors, CreateWatch())
{
    protected override string SchemaFileName => "theme.schema.json";

    protected override string ReadSchema()
    {
        return BuiltInThemes.ReadSchema();
    }

    protected override string ReadGuide()
    {
        return BuiltInThemes.ReadGuide();
    }

    protected override ThemeFile ReadTheme(ThemeId<ThemeFile> id, string path, byte[] json)
    {
        return Theming.ReadTheme(id, path, json);
    }

    protected override ThemeFile CreateUnreadableTheme(ThemeId<ThemeFile> id, string path, string reason)
    {
        return Theming.CreateUnreadableTheme(id, path, reason);
    }

    private static ThemeFolderWatch CreateWatch()
    {
        return new ThemeFolderWatch { Filters = ["*" + ThemeFile.Extension], IncludesSubfolders = false };
    }
}
