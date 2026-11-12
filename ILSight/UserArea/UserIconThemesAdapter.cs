using System.Diagnostics.CodeAnalysis;
using ILSight.Core;
using ILSight.BuiltInArea;

namespace ILSight.UserArea;

/// <summary>The seam around the user's icon themes folder, for <see cref="UserThemes{TFile, TContents, TTheme}"/>
/// of icon themes: the theme files at the top of the folder, and the fonts and pictures they name anywhere in it,
/// each read when asked, through the folder it holds; and the app's error reports.</summary>
public sealed class UserIconThemesAdapter(ErrorLog errors) : UserThemes<IconThemeFile, IconThemeContents, IconTheme>.IPorts
{
    private const string SchemaFileName = "icon-theme.schema.json";

    private readonly ThemeFolder _folder = new(CreateWatch());

    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    public List<string> ListThemeFiles(string folder)
    {
        return _folder.ListThemeFiles(folder, IconThemeFile.Extension, SchemaFileName);
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
        return _folder.TryMakeFolder(folder, SchemaFileName, BuiltInIconThemes.ReadSchema(), BuiltInIconThemes.ReadGuide(), out reason);
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
        return new ThemeFolderWatch { Filters = ["*" + IconThemeFile.Extension, "*.ttf", "*.otf", "*.svg"], IncludesSubfolders = true };
    }
}
