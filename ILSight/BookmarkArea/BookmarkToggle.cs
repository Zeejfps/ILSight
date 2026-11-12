using System.Diagnostics;

namespace ILSight.BookmarkArea;

/// <summary>What toggling the bookmark on a line did.</summary>
public abstract record BookmarkToggle
{
    private BookmarkToggle()
    {
    }

    /// <summary>Nothing: the line cannot be bookmarked.</summary>
    public sealed record Unchanged : BookmarkToggle;

    /// <summary>Bookmarked the line with <see cref="Bookmark"/>.</summary>
    public sealed record Added(Bookmark Bookmark) : BookmarkToggle;

    /// <summary>Removed <see cref="Bookmark"/>, which was in <see cref="Row"/> of the list.</summary>
    public sealed record Removed(Bookmark Bookmark, int Row) : BookmarkToggle;

    /// <summary>Does what the handler of whichever case this is does with it.</summary>
    public void Switch(Action<Unchanged> unchanged, Action<Added> added, Action<Removed> removed)
    {
        switch (this)
        {
            case Unchanged c:
                unchanged(c);
                return;
            case Added c:
                added(c);
                return;
            case Removed c:
                removed(c);
                return;
            default:
                throw new UnreachableException($"BookmarkToggle.Switch does not handle {GetType().Name}.");
        }
    }
}
