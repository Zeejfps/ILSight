using System.Reflection;
using ZGF.AppUtils;

namespace ILSight.BuiltInArea;

/// <summary>The files a built-in theme names (an icon theme's fonts and pictures): resources embedded in the app, named by
/// their resource names. Each font is drawn as the family of its id.</summary>
internal static class EmbeddedIconAssets
{
    /// <summary>The resource <paramref name="path"/> of <paramref name="resources"/> a built-in theme names, read; or
    /// that it is not there.</summary>
    public static IconAsset Read(Assembly resources, string path)
    {
        var names = resources.GetManifestResourceNames();
        if (!names.Contains(path))
        {
            return IconTheming.CreateFailedAsset(ThemeProblem.AssetUnreadable, path);
        }

        return new IconAsset(EmbeddedAssets.LoadBytes(resources, path));
    }

    /// <summary>The resources of <paramref name="resources"/> a built-in theme names, <paramref name="named"/>, each
    /// read, by the path it names.</summary>
    public static Dictionary<string, IconAsset> ReadNamed(Assembly resources, IReadOnlyList<ThemeNamedFile> named)
    {
        var read = new Dictionary<string, IconAsset>();
        foreach (var file in named)
        {
            read[file.Path] = Read(resources, file.Path);
        }
        return read;
    }
}
