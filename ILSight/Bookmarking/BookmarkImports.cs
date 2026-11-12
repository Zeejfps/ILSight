namespace ILSight.Bookmarking;

/// <summary>The bookmark imports under way: how many are still finding their bookmarks' places, and
/// those waiting, oldest first, for the user to say how to take their bookmarks. Each waits with its
/// own bookmarks, and the user is asked about one at a time.</summary>
public sealed class BookmarkImports
{
    public readonly List<PendingImport> Waiting = new();

    /// <summary>How many imports' places are being found in the background.</summary>
    public int Finding;

    /// <summary>True while any import's places are being found in the background.</summary>
    public bool IsFinding => Finding > 0;

    /// <summary>The import the user is asked about, or null.</summary>
    public PendingImport? Asking
    {
        get
        {
            if (Waiting.Count == 0)
            {
                return null;
            }

            return Waiting[0];
        }
    }
}
