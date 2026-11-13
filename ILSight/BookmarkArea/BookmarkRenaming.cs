namespace ILSight.BookmarkArea;

/// <summary>The bookmark the bookmarks panel is renaming, if any, and whether its name field is still to take
/// focus.</summary>
public sealed class BookmarkRenaming
{
    private bool _isFocusPending;

    /// <summary>The bookmark whose name is being edited; null when none is.</summary>
    public Bookmark? Bookmark { get; private set; }

    /// <summary>Starts renaming <paramref name="bookmark"/>, its name field to take focus.</summary>
    public void Start(Bookmark bookmark)
    {
        Bookmark = bookmark;
        _isFocusPending = true;
    }

    /// <summary>Ends the rename, returning the bookmark that was being renamed, or null.</summary>
    public Bookmark? End()
    {
        var bookmark = Bookmark;
        Bookmark = null;
        return bookmark;
    }

    /// <summary>Ends the rename when it is of <paramref name="bookmark"/>, which was removed.</summary>
    public void Drop(Bookmark bookmark)
    {
        if (Bookmark != bookmark)
        {
            return;
        }

        Bookmark = null;
    }

    /// <summary>Forgets the rename of a bookmark that is gone from <paramref name="bookmarks"/>, as importing in its
    /// place drops it.</summary>
    public void FollowList(IReadOnlyList<Bookmark> bookmarks)
    {
        var renaming = Bookmark;
        if (renaming is null)
        {
            return;
        }

        if (bookmarks.Contains(renaming))
        {
            return;
        }

        Bookmark = null;
    }

    /// <summary>True once after a rename starts: the name field should take focus.</summary>
    public bool TryTakeFocus()
    {
        var focus = _isFocusPending;
        _isFocusPending = false;
        return focus;
    }
}
