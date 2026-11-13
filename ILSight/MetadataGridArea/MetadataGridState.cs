using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core.Metadata;
using ILSight.MetadataArea;

namespace ILSight.MetadataGridArea;

/// <summary>Everything a <see cref="MetadataGridWidget.MetadataGridView"/> keeps between frames of one
/// grid: the columns' widths, the sort and text filters, the selected cell and the text selected in it,
/// the scroll and the selected row's decoded details. Made for the grid a tab shows, and made anew when
/// the tab shows another, so that a grid starts unsorted, unfiltered but for the flags filters kept for
/// its kind of rows, nothing selected, at the top. A view sorted or filtered off the screen's thread lands only while
/// it is the newest asked for, keeping the row selected where it shows it.</summary>
public sealed class MetadataGridState
{
    internal const int None = -1;

    private int[] _view = [];
    private int[]? _order;
    private int _viewAskedKey;
    private bool _revealWhenArranged;
    private bool _beginEdit;
    private readonly Dictionary<(int Row, int Column), int> _open = new();

    // Buffers MeasureTallRows reuses between frames, so that a frame allocates nothing for them.
    private readonly Dictionary<int, float> _tallCellsByRow = new();
    private readonly Dictionary<int, RowHeights> _tallRowsByIndex = new();
    private readonly List<TallRow> _tallRows = new();

    public required MetadataGrid Grid { get; init; }

    internal TableColumns? Columns { get; private set; }

    public required string[] Headers { get; init; }

    public required MetadataColumnFilter?[] Filters { get; init; }

    internal int SortColumn { get; private set; } = None;
    internal bool Descending { get; private set; }

    /// <summary>The key of the view shown, which differs from the newest one asked for while a sort or filter is
    /// under way off the screen's thread.</summary>
    internal int ViewKey { get; private set; }

    /// <summary>The rows sorted, before any filter; null for the table's own order.</summary>
    internal IReadOnlyList<int>? Order => _order;

    /// <summary>The sort <see cref="Order"/> is: its column (none for the table's own order) and direction.</summary>
    internal int OrderColumn { get; private set; } = None;
    internal bool OrderDescending { get; private set; }

    /// <summary>Why the newest view asked for could not be worked out; null when it could, or is under way.</summary>
    internal Exception? ArrangeFailure { get; private set; }

    /// <summary>Whether a sort or filter asked for is still being worked out.</summary>
    internal bool IsViewUnderWay => _viewAskedKey != ViewKey;

    /// <summary>Whether what is under way sorts the rows anew, rather than filtering them alone.</summary>
    internal bool IsSortUnderWay => IsViewUnderWay && (SortColumn != OrderColumn || Descending != OrderDescending);

    public required ListSelection Rows { get; init; }

    /// <summary>The current cell, selected within <see cref="Rows"/>.</summary>
    public required TableCellSelection Cell { get; init; }

    internal readonly TableScroll Scroll = new();
    internal float RowHeight { get; private set; }
    internal PendingGridScroll? Pending { get; private set; }
    internal int EditingColumn { get; private set; } = None;
    internal FlagsFlyout Flyout { get; } = new();
    internal readonly DetailTexts Details = new();

    /// <summary>How tall the row at each view index is, as of the last measuring; a row not here is no
    /// taller than one line.</summary>
    internal IReadOnlyDictionary<int, RowHeights> TallRowsByIndex => _tallRowsByIndex;

    /// <summary>The tall rows in view order, as a virtual scroll lays them out.</summary>
    internal IReadOnlyList<TallRow> TallRows => _tallRows;

    /// <summary>The rows of the grid the view shows, in its order.</summary>
    internal IReadOnlyList<int> View => _view;

    /// <summary>How many rows the view shows.</summary>
    internal int RowCount => _view.Length;

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
            return new GridPosition { TopRow = topRow, ScrollX = Scroll.X, Row = row, Column = column };
        }
    }

    /// <summary>The selected row of the grid, or <see cref="None"/>.</summary>
    internal int Selected
    {
        get
        {
            var index = Rows.Index;
            if (index < 0 || index >= _view.Length)
            {
                return None;
            }

            return _view[index];
        }
    }

    /// <summary>The current cell's column, or <see cref="None"/>.</summary>
    internal int CurrentColumn => Cell.Column;

    internal bool HasCurrentCell => Selected >= 0 && CurrentColumn >= 0;

    /// <summary>Whether the current cell is a multi-line one opened in place.</summary>
    internal bool IsOpen => _open.ContainsKey((Selected, CurrentColumn));

    /// <summary>The row of the grid the view shows at <paramref name="index"/>.</summary>
    internal int GetRowAt(int index)
    {
        return _view[index];
    }

    /// <summary>Where the view shows <paramref name="row"/>, or -1 when it doesn't.</summary>
    internal int IndexOf(int row)
    {
        return Array.IndexOf(_view, row);
    }

    internal void KeepColumns(TableColumns columns)
    {
        Columns = columns;
    }

    internal void TakeRowHeight(float rowHeight)
    {
        RowHeight = rowHeight;
    }

    /// <summary>Returns the view to <paramref name="position"/> once it has been laid out.</summary>
    public void RestorePosition(GridPosition position)
    {
        Pending = new PendingGridScroll.Restore(position);
    }

    /// <summary>Selects <paramref name="row"/> and scrolls it into view once laid out.</summary>
    public void RevealRow(int row)
    {
        Pending = new PendingGridScroll.Reveal(row);
    }

    /// <summary>What the view was asked to do once laid out, taken as done; null when nothing was.</summary>
    internal PendingGridScroll? TakePending()
    {
        var pending = Pending;
        Pending = null;
        return pending;
    }

    /// <summary>Asks to sort by <paramref name="column"/>, the other way when it already sorts by it, and to scroll
    /// the selected row back into view once sorted (<see cref="MetadataGridWidget.UpdateView"/> asks for the
    /// rows).</summary>
    internal void SortBy(int column)
    {
        Descending = SortColumn == column && !Descending;
        SortColumn = column;
        _revealWhenArranged = true;
    }

    /// <summary>Takes <paramref name="key"/> as the view asked for now; false when it was already.</summary>
    internal bool TryAskView(int key)
    {
        if (key == _viewAskedKey)
        {
            return false;
        }

        _viewAskedKey = key;
        ArrangeFailure = null;
        return true;
    }

    /// <summary>The order shown, when it is the sort asked for, for a filter alone to start from; else null.</summary>
    internal int[]? FindReusableOrder()
    {
        if (OrderColumn != SortColumn || OrderDescending != Descending)
        {
            return null;
        }

        return _order;
    }

    /// <summary>Shows <paramref name="rows"/>, the table's own, unsorted and unfiltered, as the view
    /// <paramref name="key"/>.</summary>
    internal void ShowUnarranged(int key, int[] rows)
    {
        Show(key, null, rows);
    }

    /// <summary>Shows the rows <paramref name="ask"/> asked for, worked out as <paramref name="arranged"/>, keeping
    /// the row selected where the new view shows it; nothing when a newer view was asked for meanwhile.</summary>
    internal void ShowArranged(MetadataGridViewAsk ask, MetadataGridView arranged)
    {
        if (ask.Key != _viewAskedKey)
        {
            return;
        }

        OrderColumn = ask.SortColumn;
        OrderDescending = ask.Descending;
        Show(ask.Key, arranged.Order, arranged.Rows);
    }

    /// <summary>Ends the view <paramref name="key"/> with nothing new, the grid's module closed before the work
    /// began: the rows shown left as they were; nothing when a newer view was asked for meanwhile.</summary>
    internal void StopArranging(int key)
    {
        if (key != _viewAskedKey)
        {
            return;
        }

        ViewKey = key;
        _revealWhenArranged = false;
    }

    /// <summary>Ends the view <paramref name="key"/> with <paramref name="error"/>, which the view says, the rows
    /// shown left as they were; nothing when a newer view was asked for meanwhile.</summary>
    internal void FailArranging(int key, Exception error)
    {
        if (key != _viewAskedKey)
        {
            return;
        }

        ViewKey = key;
        ArrangeFailure = error;
        _revealWhenArranged = false;
    }

    private void Show(int key, int[]? order, int[] rows)
    {
        var selected = Selected;
        ViewKey = key;
        ArrangeFailure = null;
        _order = order;
        if (order is null)
        {
            OrderColumn = None;
            OrderDescending = false;
        }

        _view = rows;
        KeepSelected(selected);
        if (_revealWhenArranged && Selected != None)
        {
            Pending = new PendingGridScroll.Reveal(Selected);
        }

        _revealWhenArranged = false;
    }

    private void KeepSelected(int row)
    {
        var index = IndexOf(row);
        if (index < 0)
        {
            Rows.Clear();
            return;
        }
        Rows.Select(index, _view.Length, reveal: false);
    }

    internal void StartEditingFilter(int column)
    {
        EditingColumn = column;
        _beginEdit = true;
    }

    internal void StopEditingFilter()
    {
        EditingColumn = None;
    }

    /// <summary>True once after <see cref="StartEditingFilter"/>: the filter's box should start editing.</summary>
    internal bool TryTakeBeginEdit()
    {
        var begin = _beginEdit;
        _beginEdit = false;
        return begin;
    }

    internal bool IsOpenAt(int row, int column)
    {
        return _open.ContainsKey((row, column));
    }

    /// <summary>Opens <paramref name="row"/>'s multi-line cell in <paramref name="column"/> in place:
    /// the cell grown downward to show every line, which leaves the rows above where they were.</summary>
    internal void ExpandCell(int row, int column)
    {
        var lines = MetadataGrids.GetText(Grid, row, column).Count(static ch => ch == '\n') + 1;
        OpenCell(row, column, lines);
    }

    /// <summary>Opens <paramref name="row"/>'s cell in <paramref name="column"/> in place, <paramref name="lines"/>
    /// lines tall.</summary>
    internal void OpenCell(int row, int column, int lines)
    {
        _open[(row, column)] = lines;
    }

    internal void CollapseCurrentCell()
    {
        _open.Remove((Selected, CurrentColumn));
    }

    /// <summary>Works out which rows are taller than one line: those with cells open in place, as tall as their
    /// longest, and the selected row with the <paramref name="details"/> under it.</summary>
    internal void MeasureTallRows(float rowHeight, float details)
    {
        _tallCellsByRow.Clear();
        _tallRowsByIndex.Clear();
        _tallRows.Clear();
        MeasureOpenCells(rowHeight);
        if (details > 0f)
        {
            _tallCellsByRow.TryAdd(Selected, rowHeight);
        }

        foreach (var (row, cells) in _tallCellsByRow)
        {
            AddTallRow(row, cells, details);
        }

        _tallRows.Sort(static (a, b) => a.Index.CompareTo(b.Index));
    }

    private void MeasureOpenCells(float rowHeight)
    {
        foreach (var (cell, lines) in _open)
        {
            var tallest = _tallCellsByRow.GetValueOrDefault(cell.Row, rowHeight);
            _tallCellsByRow[cell.Row] = MathF.Max(tallest, lines * rowHeight);
        }
    }

    private void AddTallRow(int row, float cells, float details)
    {
        var index = IndexOf(row);
        if (index < 0)
        {
            return;
        }

        var below = row == Selected ? details : 0f;
        _tallRowsByIndex[index] = new RowHeights { Cells = cells, Details = below };
        _tallRows.Add(new TallRow(index, cells + below));
    }
}
