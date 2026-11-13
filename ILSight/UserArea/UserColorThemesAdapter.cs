using System.Diagnostics.CodeAnalysis;
using ILSight.Core;
using ILSight.BuiltInArea;

namespace ILSight.UserArea;

/// <summary>The seam around the user's color themes folder, for <see cref="UserThemes{TFile, TContents, TTheme}"/>
/// of color themes: the theme files at the top of the folder (which name no other files: a file named is read as an
/// icon theme's is), read through the folder it holds; and the app's error
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

    public Outcome<byte[]> ReadThemeFile(string path)
    {
        return _folder.ReadThemeFile(path);
    }

    public IReadOnlyDictionary<string, IconAsset> ReadNamedFiles(string themePath, IReadOnlyList<string> paths)
    {
        return _folder.ReadNamedFiles(themePath, paths);
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
