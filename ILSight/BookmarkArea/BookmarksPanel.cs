using ZGF.Gui.Immediate.Widgets;

namespace ILSight.BookmarkArea;

/// <summary>What the bookmarks panel shows of the bookmarks: the one selected, the cell current in it
/// and the text selected there, and the one being renamed, with the name typed so far; and its table's columns. Its
/// owner is the panel, its lifetime the screen's, the columns' the settings' (the settings' save reads them here). The
/// panel is shown in one place, a dock panel, so it is held once (<see cref="UiState"/>); its places would be
/// independent: were it shown in a second place, each would keep its own selection and column widths.</summary>
public sealed class BookmarksPanel
{
    public BookmarksPanel()
    {
        Cells = new TableCellSelection(Selection.Rows);
    }

    public ItemSelection<Bookmark> Selection { get; } = new();

    /// <summary>The cell of the selected bookmark current in the panel, and the text selected in it.</summary>
    public TableCellSelection Cells { get; }

    public BookmarkRenamer Renamer { get; } = new();

    /// <summary>The columns of the panel's table: mark, name, location and module.</summary>
    public TableColumns Columns { get; } = new(
        new TableColumn(Fixed: 28f, Controls: true),
        new TableColumn(2f, 80f),
        new TableColumn(3f, 80f),
        new TableColumn(2f, 60f));

    /// <summary>The name typed so far for the bookmark being renamed. A field, for the edit box to take by
    /// <c>ref</c>.</summary>
    public string RenameText = "";

    /// <summary>Starts renaming <paramref name="bookmark"/>, from its name.</summary>
    public void StartRename(Bookmark bookmark)
    {
        RenameText = bookmark.Name;
        Renamer.Start(bookmark);
    }

    /// <summary>Selects the bookmark a toggle added, or passes the selection of the one it removed to
    /// the bookmark now in its row.</summary>
    public void TakeToggle(BookmarkToggle toggle, IReadOnlyList<Bookmark> bookmarks)
    {
        switch (toggle)
        {
            case BookmarkToggle.Unchanged:
                break;
            case BookmarkToggle.Added added:
                Selection.Select(added.Bookmark, bookmarks);
                break;
            case BookmarkToggle.Removed removed:
                TakeRemoved(removed.Bookmark, removed.Row, bookmarks);
                break;
        }
    }

    /// <summary>Passes the selection of <paramref name="bookmark"/>, removed from <paramref name="row"/>, to the
    /// bookmark now in its row, and ends its rename.</summary>
    public void TakeRemoved(Bookmark bookmark, int row, IReadOnlyList<Bookmark> bookmarks)
    {
        Renamer.Drop(bookmark);
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
            Selection.Clear();
            return;
        }
        Selection.Select(bookmarks[kept], bookmarks, reveal: false);
    }
}
