using System.Reflection;
using ZGF.AppUtils;

namespace ILSight;

/// <summary>The fonts and pictures of a built-in icon theme: resources embedded in the app, named by
/// their resource names. Each font is drawn as the family of its id.</summary>
internal static class EmbeddedIconAssets
{
    public static IconAssets Open(Assembly resources)
    {
        return new IconAssets(
            path =>
            {
                return Read(resources, path);
            },
            (fontId, _) =>
            {
                return fontId;
            });
    }

    private static IconAsset Read(Assembly resources, string path)
    {
        var names = resources.GetManifestResourceNames();
        if (!names.Contains(path))
        {
            return IconTheming.CreateFailedAsset(ThemeProblem.AssetUnreadable, path);
        }

        return new IconAsset(EmbeddedAssets.LoadBytes(resources, path));
    }
}
