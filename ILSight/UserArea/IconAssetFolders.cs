namespace ILSight.UserArea;

/// <summary>The fonts and pictures of a user's icon theme: files inside the folder the theme file is in,
/// named by paths relative to it. A path that is absolute or leaves the folder is refused, and one that
/// is no path at all is unreadable. Each font is drawn as a family named by its file.</summary>
public static class IconAssetFolders
{
    private const string FamilyPrefix = "icon-font:";
    private const string ParentFolder = "..";

    public static IconAssets Open(string folder)
    {
        var root = Path.GetFullPath(folder);
        return new IconAssets(
            path =>
            {
                return Read(root, path);
            },
            (_, path) =>
            {
                return FamilyPrefix + Resolve(root, path);
            });
    }

    private static IconAsset Read(string root, string path)
    {
        try
        {
            return ReadInside(root, path);
        }
        catch (Exception e) when (IsReadFailure(e))
        {
            return IconTheming.CreateFailedAsset(ThemeProblem.AssetUnreadable, $"{path}: {e.Message}");
        }
    }

    private static IconAsset ReadInside(string root, string path)
    {
        var full = Resolve(root, path);
        if (full is null)
        {
            return IconTheming.CreateFailedAsset(ThemeProblem.AssetOutsideFolder, path);
        }

        return new IconAsset(File.ReadAllBytes(full));
    }

    /// <summary>True for what reading a file can throw, a path with characters no path may hold included.</summary>
    private static bool IsReadFailure(Exception e)
    {
        return e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
    }

    /// <summary>The full path of <paramref name="path"/> inside the folder; null when it is absolute or
    /// leaves the folder.</summary>
    private static string? Resolve(string root, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return null;
        }

        var full = Path.GetFullPath(Path.Combine(root, path));
        if (!IsInside(root, full))
        {
            return null;
        }

        return full;
    }

    private static bool IsInside(string root, string full)
    {
        var relative = Path.GetRelativePath(root, full);
        if (Path.IsPathRooted(relative))
        {
            return false;
        }

        if (relative == ParentFolder)
        {
            return false;
        }

        return !relative.StartsWith(ParentFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
