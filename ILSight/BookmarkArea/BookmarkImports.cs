namespace ILSight.BookmarkArea;

/// <summary>The bookmark imports waiting, oldest first, for the user to say how to take their bookmarks. Each
/// waits with its own bookmarks, and the user is asked about one at a time.</summary>
public sealed class BookmarkImports
{
    public readonly List<PendingImport> Waiting = new();

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
