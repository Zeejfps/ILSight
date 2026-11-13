using ZGF.Gui.Immediate.Widgets;

namespace ILSight.BookmarkArea;

/// <summary>What the bookmarks panel shows of the bookmarks: the one selected, the cell current in it
/// and the text selected there, and the one being renamed, with the name typed so far.</summary>
public sealed class BookmarksPanel
{
    public BookmarksPanel()
    {
        Cells = new TableCellSelection(Selection.Rows);
    }

    public ItemSelection<Bookmark> Selection { get; } = new();

    /// <summary>The cell of the selected bookmark current in the panel, and the text selected in it.</summary>
    public TableCellSelection Cells { get; }

    public Bookmark? Selected => Selection.Item;

    public BookmarkRenaming Renaming { get; } = new();

    /// <summary>The name typed so far for the bookmark being renamed. A field, for the edit box to take by
    /// <c>ref</c>.</summary>
    public string RenameText = "";

    /// <summary>Starts renaming <paramref name="bookmark"/>, from its name.</summary>
    public void StartRename(Bookmark bookmark)
    {
        RenameText = bookmark.Name;
        Renaming.Start(bookmark);
    }

    /// <summary>Selects the bookmark a toggle added, or passes the selection of the one it removed to
    /// the bookmark now in its row.</summary>
    public void TakeToggle(BookmarkToggle toggle, IReadOnlyList<Bookmark> bookmarks)
    {
        toggle.Switch(
            unchanged: static _ => { },
            added: added => ItemSelections.Select(Selection, added.Bookmark, bookmarks),
            removed: removed => TakeRemoved(removed.Bookmark, removed.Row, bookmarks));
    }

    /// <summary>Passes the selection of <paramref name="bookmark"/>, removed from <paramref name="row"/>, to the
    /// bookmark now in its row, and ends its rename.</summary>
    public void TakeRemoved(Bookmark bookmark, int row, IReadOnlyList<Bookmark> bookmarks)
    {
        Renaming.Drop(bookmark);
        if (Selection.Item != bookmark)
        {
            return;
        }

        SelectInRow(row, bookmarks);
    }

    private void SelectInRow(int row, IReadOnlyList<Bookmark> bookmarks)
    {
        var kept = Math.Min(row, bookmarks.Count - 1);
        if (kept < 0)
        {
            ItemSelections.Clear(Selection);
            return;
        }
        ItemSelections.Select(Selection, bookmarks[kept], bookmarks, reveal: false);
    }
}
