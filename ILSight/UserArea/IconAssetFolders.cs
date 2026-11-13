namespace ILSight.UserArea;

/// <summary>The fonts and pictures of a user's icon theme: files inside the folder the theme file is in,
/// named by paths relative to it. A path that is absolute or leaves the folder is refused, and one that
/// is no path at all is unreadable. Each font is drawn as a family named by its file.</summary>
public static class IconAssetFolders
{
    private const string FamilyPrefix = "icon-font:";
    private const string ParentFolder = "..";

    /// <summary>The file at <paramref name="path"/> inside <paramref name="folder"/>, as a theme file there names it,
    /// read, with the family a font in it is drawn as; or why it cannot be read.</summary>
    public static IconAsset Read(string folder, string path)
    {
        var root = Path.GetFullPath(folder);
        try
        {
            return ReadInside(root, path);
        }
        catch (IOException e)
        {
            return IconTheming.CreateFailedAsset(ThemeProblem.AssetUnreadable, $"{path}: {e.Message}");
        }
        catch (UnauthorizedAccessException e)
        {
            return IconTheming.CreateFailedAsset(ThemeProblem.AssetUnreadable, $"{path}: {e.Message}");
        }
        catch (ArgumentException e)
        {
            return IconTheming.CreateFailedAsset(ThemeProblem.AssetUnreadable, $"{path}: {e.Message}");
        }
        catch (NotSupportedException e)
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

        return new IconAsset { Bytes = File.ReadAllBytes(full), Family = FamilyPrefix + full };
    }

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
