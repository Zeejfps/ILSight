using ZGF.Gui.Immediate.Widgets;

namespace ILSight;

/// <summary>What the bookmarks panel shows of the bookmarks: the one selected, the cell current in it
/// and the text selected there, and the one being renamed, with the name typed so far.</summary>
public sealed class BookmarksPanel
{
    public BookmarksPanel() => Cells = new TableCellSelection(Selection.Rows);

    public ItemSelection<Bookmark> Selection { get; } = new();

    /// <summary>The cell of the selected bookmark current in the panel, and the text selected in it.</summary>
    public TableCellSelection Cells { get; }

    public Bookmark? Selected => Selection.Item;

    /// <summary>The bookmark whose name is being edited, and the name typed so far.</summary>
    public Bookmark? Renaming { get; set; }

    public string RenameText = "";

    /// <summary>True once after a rename starts: the name field should take focus.</summary>
    public bool FocusRename { get; set; }
}
