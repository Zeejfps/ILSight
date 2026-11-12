using System.Text;

namespace ILSight.App.Tests;

/// <summary>Fonts and pictures held in memory by path, for icon theme files read from bytes.</summary>
internal sealed class MemoryIconAssets
{
    public const string Picture = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="10" height="10" fill="currentColor"/></svg>""";

    private readonly Dictionary<string, byte[]> _files = new();

    public MemoryIconAssets With(string path, byte[] bytes)
    {
        _files[path] = bytes;
        return this;
    }

    public MemoryIconAssets With(string path, string text)
    {
        return With(path, Encoding.UTF8.GetBytes(text));
    }

    /// <summary>The icon theme <paramref name="id"/> of <paramref name="file"/> read from <paramref name="json"/> as
    /// the kind reads one: parsed, then completed with the fonts and pictures it names, read from here.</summary>
    public IconThemeFile ReadTheme(ThemeId<IconThemeFile> id, string file, byte[] json)
    {
        var parsed = IconThemeFile.Parse(id, file, json);
        return IconThemeFile.Complete(parsed, ReadNamed(parsed.NamedFiles));
    }

    private Dictionary<string, IconAsset> ReadNamed(IReadOnlyList<ThemeNamedFile> named)
    {
        var read = new Dictionary<string, IconAsset>();
        foreach (var file in named)
        {
            read[file.Path] = Read(file.Path);
        }
        return read;
    }

    public IconAsset Read(string path)
    {
        if (_files.TryGetValue(path, out var bytes))
        {
            return new IconAsset { Bytes = bytes, Family = "memory:" + path };
        }

        return IconTheming.CreateFailedAsset(ThemeProblem.AssetUnreadable, path);
    }
}
