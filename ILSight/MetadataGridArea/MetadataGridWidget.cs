using System.Diagnostics;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core.Metadata;
using ILSight.MetadataArea;

namespace ILSight.MetadataGridArea;

public static class MetadataGridWidget
{
    private const int FunnelSalt = 0x4D444746;
    private const int FilterTextSalt = 0x4D444754;

    public static MetadataGridResult RenderMetadataGrid(Ui<UiState> ui, string id, MetadataGridState state, MetadataFilters flags,
        string? closedAssembly = null)
    {
        var listId = ui.GetId(id);
        var frame = new MetadataGridFrame
        {
            Ui = ui,
            Grid = state.Grid,
            State = state,
            Flags = flags,
            Views = ui.State.MetadataGridViews,
            Pages = ui.State.MetadataGridPages,
            Strings = ui.State.Strings,
            ListId = listId,
            MenuId = HashCode.Combine(listId, MetadataGridFrame.MenuSalt),
            ClosedAssembly = closedAssembly,
        };
        return frame.Render();
    }

    /// <summary>A view of <paramref name="grid"/> as a grid starts: unsorted, unfiltered but for the flags
    /// filters kept for its kind of rows, nothing selected, at the top.</summary>
    public static MetadataGridState CreateState(MetadataGrid grid)
    {
        var rows = new ListSelection();
        return new MetadataGridState
        {
            Grid = grid,
            Headers = ListHeaders(grid),
            Filters = new MetadataColumnFilter?[grid.Columns.Count],
            Rows = rows,
            Cell = new TableCellSelection(rows),
        };
    }

    private static string[] ListHeaders(MetadataGrid grid)
    {
        return grid.Columns
            .Select(static c => c.Name)
            .ToArray();
    }

    /// <summary>Returns the view to <paramref name="position"/> once it has been laid out.</summary>
    public static void RestorePosition(MetadataGridState state, GridPosition position)
    {
        state.Pending = new PendingGridScroll.Restore(position);
    }

    /// <summary>Selects <paramref name="row"/> and scrolls it into view once laid out.</summary>
    public static void RevealRow(MetadataGridState state, int row)
    {
        state.Pending = new PendingGridScroll.Reveal(row);
    }

    /// <summary>Asks to sort by <paramref name="column"/>, the other way when it already sorts by it, and to scroll
    /// the selected row back into view once sorted (<see cref="UpdateView"/> asks for the rows).</summary>
    internal static void SortBy(MetadataGridState state, int column)
    {
        state.Descending = state.SortColumn == column && !state.Descending;
        state.SortColumn = column;
        state.RevealWhenArranged = true;
    }

    /// <summary>Asks <paramref name="pages"/> for the page <paramref name="row"/> of <paramref name="grid"/> is on, which
    /// the view found it has not read: a paged grid's, which the frame edge reads.</summary>
    internal static void AskForPage(MetadataGridPages pages, MetadataGrid grid, int row)
    {
        if (grid is MetadataGrid.Paged paged)
        {
            pages.Ask(paged, row);
        }
    }

    /// <summary>The filter of <paramref name="column"/>, made on first use; a column of flags gets
    /// the one <paramref name="flags"/> keeps for it.</summary>
    internal static MetadataColumnFilter EnsureColumnFilter(MetadataGridState state, int column, MetadataFilters flags)
    {
        var filter = state.Filters[column];
        if (filter is not null)
        {
            return filter;
        }

        filter = CreateColumnFilter(state, column, flags);
        state.Filters[column] = filter;
        return filter;
    }

    private static MetadataColumnFilter CreateColumnFilter(MetadataGridState state, int column, MetadataFilters flags)
    {
        if (!IsFlagsColumn(state, column))
        {
            return new MetadataColumnFilter.ByText();
        }

        return new MetadataColumnFilter.ByFlags { Flags = GetFilter(flags, state.Grid, column) };
    }

    private static bool IsFlagsColumn(MetadataGridState state, int column)
    {
        return state.Grid.Columns[column].Flags != null;
    }

    /// <summary>Asks <paramref name="views"/> for the rows sorted and filtered again when a filter or the sort
    /// changed: a wait, which the frame edge starts off the screen's thread, the view showing the rows it has until
    /// they land (<see cref="ShowArranged"/>). The table's own rows, unsorted and unfiltered, read no cell, and show
    /// at once. The grid of a closed assembly (<paramref name="isClosed"/>) asks for nothing: a sort or filter stays
    /// unavailable, the view saying why.</summary>
    internal static void UpdateView(MetadataGridState state, MetadataFilters flags, MetadataGridViews views, bool isClosed)
    {
        var key = ComputeViewKey(state, flags);
        if (key == state.ViewAskedKey)
        {
            return;
        }

        state.ViewAskedKey = key;
        state.ArrangeFailure = null;
        if (state.SortColumn == MetadataGridState.None && !HasActiveFilter(state))
        {
            views.Forget(state);
            Show(state, key, null, Enumerable.Range(0, state.Grid.RowCount).ToArray());
            return;
        }

        if (isClosed)
        {
            views.Forget(state);
            return;
        }

        var order = state.OrderColumn == state.SortColumn && state.OrderDescending == state.Descending ? state.Order : null;
        views.Ask(new MetadataGridViewAsk
        {
            State = state,
            Key = key,
            SortColumn = state.SortColumn,
            Descending = state.Descending,
            Order = order,
            Filters = MetadataGrids.CopyFilters(state.Filters),
        });
    }

    private static bool HasActiveFilter(MetadataGridState state)
    {
        foreach (var filter in state.Filters)
        {
            if (filter is not null && filter.IsActive)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Shows the rows <paramref name="ask"/> asked for, worked out as <paramref name="arranged"/>, keeping
    /// the row selected where the new view shows it; nothing when a newer view was asked for meanwhile.</summary>
    internal static void ShowArranged(MetadataGridViewAsk ask, MetadataGridView arranged)
    {
        var state = ask.State;
        if (ask.Key != state.ViewAskedKey)
        {
            return;
        }

        state.OrderColumn = ask.SortColumn;
        state.OrderDescending = ask.Descending;
        Show(state, ask.Key, arranged.Order, arranged.Rows);
    }

    /// <summary>Ends what <paramref name="ask"/> asked for with nothing new, the grid's module closed before the work
    /// began: the rows it showed left as they were; nothing when a newer view was asked for meanwhile.</summary>
    internal static void ShowArrangeStopped(MetadataGridViewAsk ask)
    {
        var state = ask.State;
        if (ask.Key != state.ViewAskedKey)
        {
            return;
        }

        state.ViewKey = ask.Key;
        state.RevealWhenArranged = false;
    }

    /// <summary>Ends what <paramref name="ask"/> asked for with <paramref name="error"/>, which the view says, the rows
    /// it showed left as they were; nothing when a newer view was asked for meanwhile.</summary>
    internal static void ShowArrangeFailed(MetadataGridViewAsk ask, Exception error)
    {
        var state = ask.State;
        if (ask.Key != state.ViewAskedKey)
        {
            return;
        }

        state.ViewKey = ask.Key;
        state.ArrangeFailure = error;
        state.RevealWhenArranged = false;
    }

    private static void Show(MetadataGridState state, int key, int[]? order, int[] rows)
    {
        var selected = state.Selected;
        state.ViewKey = key;
        state.ArrangeFailure = null;
        state.Order = order;
        if (order is null)
        {
            state.OrderColumn = MetadataGridState.None;
            state.OrderDescending = false;
        }

        state.View = rows;
        KeepSelected(state, selected);
        if (state.RevealWhenArranged && state.Selected != MetadataGridState.None)
        {
            state.Pending = new PendingGridScroll.Reveal(state.Selected);
        }

        state.RevealWhenArranged = false;
    }

    /// <summary>Selects <paramref name="row"/> where the new view shows it; nothing when it doesn't.</summary>
    private static void KeepSelected(MetadataGridState state, int row)
    {
        var index = GetIndexOf(state, row);
        if (index < 0)
        {
            state.Rows.Clear();
            return;
        }
        state.Rows.Select(index, state.View.Length, reveal: false);
    }

    private static int ComputeViewKey(MetadataGridState state, MetadataFilters flags)
    {
        var key = new HashCode();
        key.Add(state.SortColumn);
        key.Add(state.Descending);
        for (var c = 0; c < state.Grid.Columns.Count; c++)
        {
            // A column of flags is filtered by what is kept for it, whether or not its heading was drawn yet.
            var filter = IsFlagsColumn(state, c) ? EnsureColumnFilter(state, c, flags) : state.Filters[c];
            key.Add(filter is null ? 0 : filter.Match(static typed => typed.Text.GetHashCode(), static picked => picked.Flags.Revision.GetHashCode()));
        }
        return key.ToHashCode() | 1;
    }

    internal static void StartEditingFilter(MetadataGridState state, int column)
    {
        state.EditingColumn = column;
        state.BeginEdit = true;
    }

    internal static void StopEditingFilter(MetadataGridState state)
    {
        state.EditingColumn = MetadataGridState.None;
    }

    internal static void ToggleFlagsFlyout(MetadataGridState state, int column)
    {
        state.FlagsOpen = !IsFlagsFlyoutOpenFor(state, column);
        state.FlagsColumn = column;
    }

    internal static bool IsFlagsFlyoutOpenFor(MetadataGridState state, int column)
    {
        return state.FlagsOpen && state.FlagsColumn == column;
    }

    /// <summary>Makes <paramref name="row"/>'s cell in <paramref name="column"/> the current one
    /// with all its text selected, where the view shows the row.</summary>
    internal static void SelectCell(MetadataGridState state, int row, int column)
    {
        var index = GetIndexOf(state, row);
        if (index < 0)
        {
            return;
        }

        state.Cell.Select(index, state.View.Length, column, reveal: false);
    }

    internal static bool IsOpenAt(MetadataGridState state, int row, int column)
    {
        return state.Open.ContainsKey((row, column));
    }

    /// <summary>Opens <paramref name="row"/>'s multi-line cell in <paramref name="column"/> in place:
    /// the cell grown downward to show every line, which leaves the rows above where they were.</summary>
    internal static void ExpandCell(MetadataGridState state, int row, int column)
    {
        var lines = MetadataGrids.GetText(state.Grid, row, column).Count(static ch => ch == '\n') + 1;
        state.Open[(row, column)] = lines;
    }

    internal static void CollapseCurrentCell(MetadataGridState state)
    {
        state.Open.Remove((state.Selected, state.CurrentColumn));
    }

    /// <summary>The current cell's text as shown: all of it while open, else its first line.</summary>
    internal static string GetShownText(MetadataGridState state)
    {
        return GetShownTextAt(state, state.Selected, state.CurrentColumn);
    }

    internal static void SelectAllText(MetadataGridState state)
    {
        state.Cell.SelectAllText();
    }

    internal static string GetShownTextAt(MetadataGridState state, int row, int column)
    {
        if (row < 0)
        {
            return "";
        }

        if (column < 0)
        {
            return "";
        }

        if (IsOpenAt(state, row, column))
        {
            return MetadataGrids.GetText(state.Grid, row, column);
        }

        return MetadataGrids.GetLines(state.Grid, row, column).FirstLine;
    }

    /// <summary>What copying takes: the current cell's text, or the whole row's without one.</summary>
    internal static string GetCopiedText(MetadataGridState state)
    {
        if (state.CurrentColumn < 0)
        {
            return GetRowText(state, state.Selected);
        }

        return GetCellText(state);
    }

    /// <summary>What copying the current cell takes: the part of its text selected, else all of it,
    /// lines hidden while it is closed included.</summary>
    internal static string GetCellText(MetadataGridState state)
    {
        return state.Cell.Copied(GetShownText(state), MetadataGrids.GetText(state.Grid, state.Selected, state.CurrentColumn));
    }

    internal static string GetRowText(MetadataGridState state, int row)
    {
        var grid = state.Grid;
        var cells = Enumerable.Range(0, grid.Columns.Count)
            .Select(c => MetadataGrids.GetText(grid, row, c));
        return string.Join('\t', cells);
    }

    internal static int GetRowAt(MetadataGridState state, int index)
    {
        return state.View[index];
    }

    internal static int GetIndexOf(MetadataGridState state, int row)
    {
        return Array.IndexOf(state.View, row);
    }

    /// <summary>Filters starting from a copy of <paramref name="saved"/>, what the settings kept.</summary>
    public static MetadataFilters CreateFilters(Dictionary<string, SavedFilterState>? saved)
    {
        var copied = saved is not null ? new Dictionary<string, SavedFilterState>(saved) : new Dictionary<string, SavedFilterState>();
        return new MetadataFilters { Saved = copied };
    }

    /// <summary>The filter of <paramref name="column"/>, a column of flags, as last left.</summary>
    public static FilterState GetFilter(MetadataFilters filters, MetadataGrid grid, int column)
    {
        var key = GetKey(grid, column);
        if (filters.States.TryGetValue(key, out var state))
        {
            return state;
        }

        var schema = grid.Columns[column].Flags;
        if (schema == null)
        {
            throw new ArgumentException($"{key} is not a flags column.", nameof(column));
        }

        state = FilterStatePresenter.CreateFilterState(schema);
        if (filters.Saved.TryGetValue(key, out var saved))
        {
            FilterStatePresenter.RestoreFilter(state, saved);
        }

        filters.States.Add(key, state);
        return state;
    }

    private static string GetKey(MetadataGrid grid, int column)
    {
        return $"{grid.Key}/{grid.Columns[column].Name}";
    }

    /// <summary>Every filter that filters something, to save.</summary>
    public static Dictionary<string, SavedFilterState> ListSaved(MetadataFilters filters)
    {
        foreach (var (key, state) in filters.States)
        {
            var saved = FilterStatePresenter.SaveFilter(state);
            if (saved != null)
            {
                filters.Saved[key] = saved;
            }
            else
            {
                filters.Saved.Remove(key);
            }
        }
        return new Dictionary<string, SavedFilterState>(filters.Saved);
    }

    /// <summary>Works out which rows of <paramref name="state"/> are taller than one line: those with cells
    /// open in place, as tall as their longest, and the selected row with the <paramref name="details"/>
    /// under it.</summary>
    internal static void MeasureTallRows(MetadataGridState state, float rowHeight, float details)
    {
        state.TallCellsByRow.Clear();
        state.TallRowsByIndex.Clear();
        state.TallRows.Clear();
        MeasureOpenCells(state, rowHeight);
        if (details > 0f)
        {
            state.TallCellsByRow.TryAdd(state.Selected, rowHeight);
        }

        foreach (var (row, cells) in state.TallCellsByRow)
        {
            AddTallRow(state, row, cells, details);
        }

        state.TallRows.Sort(static (a, b) => a.Index.CompareTo(b.Index));
    }

    private static void MeasureOpenCells(MetadataGridState state, float rowHeight)
    {
        foreach (var (cell, lines) in state.Open)
        {
            var tallest = state.TallCellsByRow.GetValueOrDefault(cell.Row, rowHeight);
            state.TallCellsByRow[cell.Row] = MathF.Max(tallest, lines * rowHeight);
        }
    }

    private static void AddTallRow(MetadataGridState state, int row, float cells, float details)
    {
        var index = GetIndexOf(state, row);
        if (index < 0)
        {
            return;
        }

        var below = row == state.Selected ? details : 0f;
        state.TallRowsByIndex[index] = new RowHeights { Cells = cells, Details = below };
        state.TallRows.Add(new TallRow(index, cells + below));
    }

    internal static int GetFunnelId(int listId, int column)
    {
        return HashCode.Combine(listId, FunnelSalt, column);
    }

    /// <summary>Draws the heading of <paramref name="column"/>, as a table asks of its headings.</summary>
    internal static void RenderHeading(Ui ui, int column, bool hovered, in MetadataGridHeading heading)
    {
        var filter = EnsureColumnFilter(heading.State, column, heading.Flags);
        var textId = HashCode.Combine(heading.ListId, FilterTextSalt, column);
        DrawHeadingText(ui, column, hovered, in heading, filter, textId);
        DrawFunnel(ui, in heading, filter, column);
    }

    /// <summary>What the heading shows of its column: a text filter being edited or typed, else the column's name.</summary>
    private static void DrawHeadingText(Ui ui, int column, bool hovered, in MetadataGridHeading heading, MetadataColumnFilter filter, int textId)
    {
        if (filter is MetadataColumnFilter.ByText text)
        {
            DrawTextFilterHeading(ui, column, hovered, in heading, text, textId);
            return;
        }

        DrawColumnName(ui, heading.Grid.Columns[column].Name, hovered);
    }

    private static void DrawTextFilterHeading(Ui ui, int column, bool hovered, in MetadataGridHeading heading, MetadataColumnFilter.ByText text,
        int textId)
    {
        if (heading.State.EditingColumn == column)
        {
            EditFilterText(ui, heading.State, text, textId);
            return;
        }

        if (text.Text.Length > 0)
        {
            DrawFilterText(ui, heading.State, text, textId, column);
            return;
        }

        DrawColumnName(ui, heading.Grid.Columns[column].Name, hovered);
    }

    private static void EditFilterText(Ui ui, MetadataGridState state, MetadataColumnFilter.ByText filter, int textId)
    {
        var text = filter.Text;
        using (ui.Id(textId))
        {
            var edit = ui.InlineEdit("filter", ref text, state.BeginEdit, Sizing.Grow());
            state.BeginEdit = false;
            filter.Text = edit == InlineEditResult.Cancelled ? "" : text;
            if (edit != InlineEditResult.Editing)
            {
                StopEditingFilter(state);
            }
        }
    }

    private static void DrawFilterText(Ui ui, MetadataGridState state, MetadataColumnFilter.ByText filter, int textId, int column)
    {
        var theme = ui.Theme;
        var shown = ui.Interact(textId);
        if (shown.Hovered)
        {
            ui.SetCursor(MouseCursor.Text);
        }

        if (shown.Clicked)
        {
            StartEditingFilter(state, column);
        }

        ui.Text(filter.Text, new Box { Width = Sizing.Grow() }, color: theme.AccentText, size: theme.FontSizeSmall, id: shown.Id,
            overflow: TextOverflow.Ellipsis);
    }

    private static void DrawColumnName(Ui ui, string name, bool hovered)
    {
        var theme = ui.Theme;
        var color = hovered ? theme.Text : theme.TextMuted;
        ui.Text(name, new Box { Width = Sizing.Grow() }, color: color, size: theme.FontSizeSmall, overflow: TextOverflow.Ellipsis);
    }

    private static void DrawFunnel(Ui ui, in MetadataGridHeading heading, MetadataColumnFilter filter, int column)
    {
        var theme = ui.Theme;
        var funnel = ui.Clickable(GetFunnelId(heading.ListId, column));
        if (funnel.Clicked)
        {
            ClickFunnel(heading.State, filter, column);
        }

        var glyph = GetFunnelGlyph(heading.Icons, filter);
        var color = GetFunnelColor(theme, filter, funnel.Hovered);
        using (ui.Box(funnel.Id, new Box { Width = theme.IconSize, Height = Sizing.Grow(), AlignX = Align.Center, AlignY = Align.Center }))
        {
            ui.Glyph(glyph, color, theme.FontSizeSmall);
        }

        if (!IsFlagsFlyoutOpenFor(heading.State, column))
        {
            ui.Tooltip(funnel.Id, GetFunnelTooltip(ui, heading.Strings, funnel.Id, heading.Grid.Columns[column].Name));
        }
    }

    private static string GetFunnelTooltip(Ui ui, Strings t, int funnelId, string name)
    {
        using (ui.Id(funnelId))
        {
            return ui.Memo("tooltip", (name, t), static a => a.t.MetadataFilter(a.name));
        }
    }

    private static void ClickFunnel(MetadataGridState state, MetadataColumnFilter filter, int column)
    {
        filter.Switch(
            text => ClickTextFunnel(state, text, column),
            _ => ToggleFlagsFlyout(state, column));
    }

    /// <summary>Clears a typed filter, else starts typing one.</summary>
    private static void ClickTextFunnel(MetadataGridState state, MetadataColumnFilter.ByText text, int column)
    {
        if (text.Text.Length > 0)
        {
            text.Text = "";
            StopEditingFilter(state);
            return;
        }

        StartEditingFilter(state, column);
    }

    private static IconGlyph GetFunnelGlyph(AppIcons icons, MetadataColumnFilter filter)
    {
        if (filter is MetadataColumnFilter.ByText text && text.Text.Length > 0)
        {
            return icons.MetadataClearFilter;
        }

        return icons.MetadataFilter;
    }

    private static uint GetFunnelColor(UiTheme theme, MetadataColumnFilter filter, bool hovered)
    {
        if (filter.IsActive)
        {
            return theme.AccentText;
        }

        if (hovered)
        {
            return theme.Text;
        }

        return theme.TextMuted;
    }
}
