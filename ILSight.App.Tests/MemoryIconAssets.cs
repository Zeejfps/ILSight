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

    public static implicit operator IconAssets(MemoryIconAssets assets)
    {
        return new IconAssets(assets.Read, assets.GetFamily);
    }

    public IconAsset Read(string path)
    {
        if (_files.TryGetValue(path, out var bytes))
        {
            return new IconAsset(bytes);
        }

        return IconTheming.CreateFailedAsset(ThemeProblem.AssetUnreadable, path);
    }

    public string GetFamily(string fontId, string path)
    {
        return "memory:" + path;
    }
}
