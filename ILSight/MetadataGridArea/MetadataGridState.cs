using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core.Metadata;
using ILSight.MetadataArea;

namespace ILSight.MetadataGridArea;

/// <summary>Everything a <see cref="MetadataGridWidget.MetadataGridView"/> keeps between frames of one
/// grid: the columns' widths, the sort and text filters, the selected cell and the text selected in it,
/// the scroll and the selected row's decoded details. Made for the grid a tab shows, and made anew when
/// the tab shows another, so that a grid starts unsorted, unfiltered but for the flags filters kept for
/// its kind of rows, nothing selected, at the top.</summary>
public sealed class MetadataGridState
{
    internal const int None = -1;

    public required MetadataGrid Grid { get; init; }

    internal TableColumns? Columns;

    public required string[] Headers { get; init; }

    public required MetadataColumnFilter?[] Filters { get; init; }

    internal int SortColumn = None;
    internal bool Descending;
    internal int[]? Order;
    internal int[] View = [];

    /// <summary>The key of the view shown (<see cref="View"/>), and of the newest one asked for, which differ while
    /// a sort or filter is under way off the screen's thread.</summary>
    internal int ViewKey;
    internal int ViewAskedKey;

    /// <summary>The sort <see cref="Order"/> is: its column (none for the table's own order) and direction.</summary>
    internal int OrderColumn = None;
    internal bool OrderDescending;

    /// <summary>Whether the selected row is scrolled back into view when the view asked for lands, as after a
    /// sort.</summary>
    internal bool RevealWhenArranged;

    /// <summary>Why the newest view asked for could not be worked out; null when it could, or is under way.</summary>
    internal Exception? ArrangeFailure;

    /// <summary>Whether a sort or filter asked for is still being worked out.</summary>
    internal bool IsViewUnderWay => ViewAskedKey != ViewKey;

    /// <summary>Whether what is under way sorts the rows anew, rather than filtering them alone.</summary>
    internal bool IsSortUnderWay => IsViewUnderWay && (SortColumn != OrderColumn || Descending != OrderDescending);

    public required ListSelection Rows { get; init; }

    /// <summary>The current cell, selected within <see cref="Rows"/>.</summary>
    public required TableCellSelection Cell { get; init; }

    internal readonly TableScroll Scroll = new();
    internal float RowHeight;
    internal PendingGridScroll? Pending;
    internal int EditingColumn = None;
    internal bool BeginEdit;
    internal int FlagsColumn = None;
    internal bool FlagsOpen;
    internal readonly Dictionary<(int Row, int Column), int> Open = new();
    internal readonly DetailTexts Details = new();

    /// <summary>The cells' height of each row taller than one line, while they are measured; see
    /// <see cref="MetadataGridWidget.MeasureTallRows"/>, which reuses these buffers between frames.</summary>
    internal readonly Dictionary<int, float> TallCellsByRow = new();

    /// <summary>How tall the row at each view index is, as of the last measuring; a row not here is no
    /// taller than one line.</summary>
    internal readonly Dictionary<int, RowHeights> TallRowsByIndex = new();

    /// <summary>The tall rows in view order, as a virtual scroll lays them out.</summary>
    internal readonly List<TallRow> TallRows = new();

    /// <summary>Where the view stands: the top row, the sideways scroll, and the selected row and current cell's
    /// column; where it is to be returned to while that waits for it to be laid out.</summary>
    public GridPosition Position
    {
        get
        {
            if (Pending is PendingGridScroll.Restore restoring)
            {
                return restoring.Position;
            }

            var topRow = RowHeight > 0f ? (int)(Scroll.Y / RowHeight) : 0;
            int? row = Selected == None ? null : Selected;
            int? column = CurrentColumn == None ? null : CurrentColumn;
            return new GridPosition(topRow, Scroll.X, row, column);
        }
    }

    /// <summary>The selected row of the grid, or <see cref="None"/>.</summary>
    internal int Selected
    {
        get
        {
            var index = Rows.Index;
            if (index < 0 || index >= View.Length)
            {
                return None;
            }

            return View[index];
        }
    }

    /// <summary>The current cell's column, or <see cref="None"/>.</summary>
    internal int CurrentColumn => Cell.Column;

    internal bool HasCurrentCell => Selected >= 0 && CurrentColumn >= 0;

    /// <summary>Whether the current cell is a multi-line one opened in place.</summary>
    internal bool IsOpen => Open.ContainsKey((Selected, CurrentColumn));
}
