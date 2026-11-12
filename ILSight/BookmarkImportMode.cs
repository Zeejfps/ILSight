namespace ILSight;

/// <summary>What importing bookmarks does with the ones there are.</summary>
public enum BookmarkImportMode
{
    /// <summary>Drops them for the imported ones.</summary>
    Replace,

    /// <summary>Keeps them, adding the imported ones that mark a line none of them does.</summary>
    Merge,
}
