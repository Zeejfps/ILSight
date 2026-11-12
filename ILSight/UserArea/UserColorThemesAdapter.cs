using System.Diagnostics.CodeAnalysis;
using ILSight.BuiltInArea;

namespace ILSight.UserArea;

/// <summary>The seam around the user's color themes folder, for <see cref="UserThemes{TFile, TContents, TTheme}"/>
/// of color themes: the theme files at the top of the folder, read through the folder it holds; and the app's error
/// reports.</summary>
public sealed class UserColorThemesAdapter(ErrorLog errors) : UserThemes<ThemeFile, ThemeContents, AppTheme>.IPorts
{
    private const string SchemaFileName = "theme.schema.json";

    private readonly ThemeFolder _folder = new(CreateWatch());

    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    public List<string> ListThemeFiles(string folder)
    {
        return _folder.ListThemeFiles(folder, ThemeFile.Extension, SchemaFileName);
    }

    /// <summary>Reads the whole file now, so nothing is left to fail later.</summary>
    public ThemeFile ReadThemeFile(ThemeId<ThemeFile> id, string path)
    {
        byte[] json;
        try
        {
            json = _folder.ReadFile(path);
        }
        catch (IOException e)
        {
            return Theming.CreateUnreadableTheme(id, path, e.Message);
        }
        catch (UnauthorizedAccessException e)
        {
            return Theming.CreateUnreadableTheme(id, path, e.Message);
        }

        return Theming.ReadTheme(id, path, json);
    }

    public bool TryMakeFolder(string folder, [NotNullWhen(false)] out string? reason)
    {
        return _folder.TryMakeFolder(folder, SchemaFileName, BuiltInThemes.ReadSchema(), BuiltInThemes.ReadGuide(), out reason);
    }

    public bool IsFile(string path)
    {
        return _folder.IsFile(path);
    }

    public bool TryWriteNewFile(string path, string text, [NotNullWhen(false)] out string? reason)
    {
        return _folder.TryWriteNewFile(path, text, out reason);
    }

    public bool TryStartWatching(string folder, [NotNullWhen(false)] out string? reason)
    {
        return _folder.TryStartWatching(folder, out reason);
    }

    public Task<Outcome<ThemeFolderEvent>> WaitForChangeAsync(CancellationToken ct)
    {
        return _folder.WaitForChangeAsync(ct);
    }

    private static ThemeFolderWatch CreateWatch()
    {
        return new ThemeFolderWatch { Filters = ["*" + ThemeFile.Extension], IncludesSubfolders = false };
    }
}
