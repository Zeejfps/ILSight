using System.Diagnostics;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.Localization;
using ZGF.KeyboardModule;

namespace ILSight;

/// <summary>What the user asked of a <see cref="MetadataGridWidget.RenderMetadataGrid"/> this frame.</summary>
/// <param name="Target">The table row a token cell leads to.</param>
/// <param name="Definition">What a row stands for in the code, to decompile.</param>
/// <param name="InNewTab">Whether to show either in a new tab.</param>
public readonly record struct MetadataGridResult(MetadataAddress? Target, CodeReference.Navigable? Definition, bool InNewTab);

public static class MetadataGridWidget
{
    private const int FunnelSalt = 0x4D444746;
    private const int FilterTextSalt = 0x4D444754;

    public static MetadataGridResult RenderMetadataGrid(Ui<UiState> ui, string id, MetadataGridState state, MetadataFilters flags)
    {
        var listId = ui.GetId(id);
        var frame = new MetadataGridFrame
        {
            Ui = ui,
            Grid = state.Grid,
            State = state,
            Flags = flags,
            Strings = ui.State.Strings,
            ListId = listId,
            MenuId = HashCode.Combine(listId, MetadataGridFrame.MenuSalt),
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

    /// <summary>Sorts by <paramref name="column"/>, the other way when it already sorts by it, and
    /// scrolls the selected row back into view.</summary>
    internal static void SortBy(MetadataGridState state, int column)
    {
        state.Descending = state.SortColumn == column && !state.Descending;
        state.SortColumn = column;
        state.Order = MetadataGrids.OrderRows(state.Grid, column, state.Descending);
        if (state.Selected != MetadataGridState.None)
            state.Pending = new PendingGridScroll.Reveal(state.Selected);
    }

    /// <summary>The filter of <paramref name="column"/>, made on first use; a column of flags gets
    /// the one <paramref name="flags"/> keeps for it.</summary>
    internal static MetadataColumnFilter EnsureColumnFilter(MetadataGridState state, int column, MetadataFilters flags)
    {
        var filter = state.Filters[column];
        if (filter != null)
            return filter;
        filter = CreateColumnFilter(state, column, flags);
        state.Filters[column] = filter;
        return filter;
    }

    private static MetadataColumnFilter CreateColumnFilter(MetadataGridState state, int column, MetadataFilters flags)
    {
        if (!IsFlagsColumn(state, column))
            return new MetadataColumnFilter.ByText();
        return new MetadataColumnFilter.ByFlags { Flags = FindFilter(flags, state.Grid, column) };
    }

    private static bool IsFlagsColumn(MetadataGridState state, int column)
    {
        return state.Grid.Columns[column].Flags != null;
    }

    /// <summary>Filters and sorts the rows again when a filter or the sort changed.</summary>
    internal static void UpdateView(MetadataGridState state, MetadataFilters flags)
    {
        var key = ComputeViewKey(state, flags);
        if (key == state.ViewKey) return;
        var selected = state.Selected;
        state.ViewKey = key;
        state.View = MetadataGrids.FilterRows(state.Grid, state.Order, state.Filters);
        KeepSelected(state, selected);
    }

    /// <summary>Selects <paramref name="row"/> where the new view shows it; nothing when it doesn't.</summary>
    private static void KeepSelected(MetadataGridState state, int row)
    {
        var index = FindIndexOf(state, row);
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
            switch (filter)
            {
                case MetadataColumnFilter.ByFlags picked:
                    key.Add(picked.Flags.Revision);
                    break;
                case MetadataColumnFilter.ByText typed:
                    key.Add(typed.Text);
                    break;
                case null:
                    key.Add(0);
                    break;
                default:
                    throw new UnreachableException();
            }
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
        var index = FindIndexOf(state, row);
        if (index < 0) return;
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
        if (row < 0) return "";
        if (column < 0) return "";
        if (IsOpenAt(state, row, column)) return MetadataGrids.GetText(state.Grid, row, column);
        return MetadataGrids.GetLines(state.Grid, row, column).FirstLine;
    }

    /// <summary>What copying takes: the current cell's text, or the whole row's without one.</summary>
    internal static string GetCopiedText(MetadataGridState state)
    {
        if (state.CurrentColumn < 0) return GetRowText(state, state.Selected);
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

    internal static int FindIndexOf(MetadataGridState state, int row)
    {
        return Array.IndexOf(state.View, row);
    }

    /// <summary>Filters starting from a copy of <paramref name="saved"/>, what the settings kept.</summary>
    public static MetadataFilters CreateFilters(Dictionary<string, SavedFilterState>? saved)
    {
        var copied = saved != null ? new Dictionary<string, SavedFilterState>(saved) : new Dictionary<string, SavedFilterState>();
        return new MetadataFilters { Saved = copied };
    }

    /// <summary>The filter of <paramref name="column"/>, a column of flags, as last left.</summary>
    public static FilterState FindFilter(MetadataFilters filters, MetadataGrid grid, int column)
    {
        var key = GetKey(grid, column);
        if (filters.States.TryGetValue(key, out var state)) return state;
        var schema = grid.Columns[column].Flags;
        if (schema == null)
            throw new ArgumentException($"{key} is not a flags column.", nameof(column));
        state = FilterStatePresenter.CreateFilterState(schema);
        if (filters.Saved.TryGetValue(key, out var saved))
            FilterStatePresenter.RestoreFilter(state, saved);
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
                filters.Saved[key] = saved;
            else
                filters.Saved.Remove(key);
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
            state.TallCellsByRow.TryAdd(state.Selected, rowHeight);
        foreach (var (row, cells) in state.TallCellsByRow)
            AddTallRow(state, row, cells, details);
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
        var index = FindIndexOf(state, row);
        if (index < 0) return;
        var below = row == state.Selected ? details : 0f;
        state.TallRowsByIndex[index] = new RowHeights(cells, below);
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
        switch (filter)
        {
            case MetadataColumnFilter.ByText text when heading.State.EditingColumn == column:
                EditFilterText(ui, heading.State, text, textId);
                break;
            case MetadataColumnFilter.ByText text when text.Text.Length > 0:
                DrawFilterText(ui, heading.State, text, textId, column);
                break;
            case MetadataColumnFilter.ByText or MetadataColumnFilter.ByFlags:
                DrawColumnName(ui, heading.Grid.Columns[column].Name, hovered);
                break;
            default:
                throw new UnreachableException();
        }
        DrawFunnel(ui, in heading, filter, column);
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
                StopEditingFilter(state);
        }
    }

    private static void DrawFilterText(Ui ui, MetadataGridState state, MetadataColumnFilter.ByText filter, int textId, int column)
    {
        var theme = ui.Theme;
        var shown = ui.Interact(textId);
        if (shown.Hovered)
            ui.SetCursor(MouseCursor.Text);
        if (shown.Clicked)
            StartEditingFilter(state, column);
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
            ClickFunnel(heading.State, filter, column);
        var glyph = GetFunnelGlyph(heading.Icons, filter);
        var color = GetFunnelColor(theme, filter, funnel.Hovered);
        using (ui.Box(funnel.Id, new Box { Width = theme.IconSize, Height = Sizing.Grow(), AlignX = Align.Center, AlignY = Align.Center }))
            ui.Glyph(glyph, color, theme.FontSizeSmall);
        if (!IsFlagsFlyoutOpenFor(heading.State, column))
            ui.Tooltip(funnel.Id, GetFunnelTooltip(ui, heading.Strings, funnel.Id, heading.Grid.Columns[column].Name));
    }

    private static string GetFunnelTooltip(Ui ui, Strings t, int funnelId, string name)
    {
        using (ui.Id(funnelId))
            return ui.Memo("tooltip", (name, t), static a => a.t.MetadataFilter(a.name));
    }

    private static void ClickFunnel(MetadataGridState state, MetadataColumnFilter filter, int column)
    {
        switch (filter)
        {
            case MetadataColumnFilter.ByFlags:
                ToggleFlagsFlyout(state, column);
                break;
            case MetadataColumnFilter.ByText text when text.Text.Length > 0:
                text.Text = "";
                StopEditingFilter(state);
                break;
            case MetadataColumnFilter.ByText:
                StartEditingFilter(state, column);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static IconGlyph GetFunnelGlyph(AppIcons icons, MetadataColumnFilter filter)
    {
        return filter switch
        {
            MetadataColumnFilter.ByText text when text.Text.Length > 0 => icons.MetadataClearFilter,
            MetadataColumnFilter.ByText or MetadataColumnFilter.ByFlags => icons.MetadataFilter,
            _ => throw new UnreachableException(),
        };
    }

    private static uint GetFunnelColor(UiTheme theme, MetadataColumnFilter filter, bool hovered)
    {
        if (filter.IsActive)
            return theme.AccentText;
        if (hovered)
            return theme.Text;
        return theme.TextMuted;
    }
}

/// <summary>One frame of a <see cref="MetadataGridWidget.RenderMetadataGrid"/>: what every step of
/// drawing it shares, kept on the stack.</summary>
internal ref struct MetadataGridFrame
{
    private const int AutoFitRows = 200;
    private const float MaxAutoWidth = 480f;
    private const float FlyoutMinWidth = 320f;
    private const int FlyoutSalt = 0x4D44464C;
    public const int MenuSalt = 0x4D44474D;

    public required Ui<UiState> Ui { get; init; }
    public required MetadataGrid Grid { get; init; }
    public required MetadataGridState State { get; init; }
    public required MetadataFilters Flags { get; init; }
    public required Strings Strings { get; init; }
    public required int ListId { get; init; }
    public required int MenuId { get; init; }
    private bool _focused;
    private MetadataGridResult _result;

    public MetadataGridResult Render()
    {
        var columns = State.Columns ??= Measure();
        State.RowHeight = Ui.Theme.RowHeight;
        MetadataGridWidget.UpdateView(State, Flags);

        using var pane = Ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f, Background = Ui.Theme.PaneBackground });
        var table = Ui.Table(ListId, columns, State.Cell, State.Scroll);
        Header(table);
        FlagsFlyout();
        MetadataGridWidget.UpdateView(State, Flags);

        _focused = table.Focused;
        table.TakeKeys(State.View.Length);
        if (_focused)
            _result = Keys();
        Rows(table, columns);
        var picked = Menu();
        if (picked != null)
            _result = picked.Value;
        return _result;
    }

    private TableColumns Measure()
    {
        var theme = Ui.Theme;
        var padding = theme.GapTight * 2;
        var widths = new float[Grid.Columns.Count];
        var rows = Math.Min(Grid.RowCount, AutoFitRows);
        for (var c = 0; c < widths.Length; c++)
        {
            var width = MeasureColumn(c, rows);
            widths[c] = MathF.Ceiling(MathF.Min(MaxAutoWidth, width + padding + theme.GapTight));
        }
        return TableColumns.Sized(widths);
    }

    private float MeasureColumn(int c, int rows)
    {
        var theme = Ui.Theme;
        var column = Grid.Columns[c];
        float? size = null;
        string? family = null;
        if (IsMono(column))
        {
            size = theme.FontSize;
            family = MonoFonts.Code;
        }
        var width = Ui.MeasureText(column.Name, theme.FontSizeSmall) + theme.IconSize * 2 + theme.GapTight * 2;
        for (var row = 0; row < rows; row++)
            width = MathF.Max(width, Ui.MeasureText(MetadataGrids.GetText(Grid, row, c), size, family: family));
        return width;
    }

    private static bool IsMono(MetadataColumn column) => column.IsNumeric || column.IsToken;

    private void Header(TableFrame table)
    {
        var icons = Ui.State.Icons;
        var sortGlyph = State.Descending ? icons.SortDescending : icons.SortAscending;
        var sort = new TableSort(State.SortColumn, new UiIcon(sortGlyph));
        var heading = new MetadataGridHeading { Grid = Grid, State = State, Flags = Flags, Strings = Strings, Icons = icons, ListId = ListId };
        var clicked = table.Header(State.Headers, sort, MetadataGridWidget.RenderHeading, heading);
        if (clicked < 0) return;
        if (clicked == State.EditingColumn) return;
        MetadataGridWidget.SortBy(State, clicked);
    }

    private void FlagsFlyout()
    {
        if (State.FlagsColumn < 0) return;
        var theme = Ui.Theme;
        var filter = MetadataGridWidget.FindFilter(Flags, Grid, State.FlagsColumn);
        var width = FlagsFilterView.ContentWidth + theme.TooltipPaddingX * 2 + theme.ScrollBarHitWidth;
        var funnelId = MetadataGridWidget.GetFunnelId(ListId, State.FlagsColumn);
        using var flyout = Ui.Flyout(HashCode.Combine(ListId, FlyoutSalt), ref State.FlagsOpen, funnelId,
            new Box { Width = Sizing.Fit(FlyoutMinWidth, width), Gap = theme.Gap });
        if (!flyout.IsOpen) return;
        FlagsFilterView.Render(Ui, filter);
    }

    private MetadataGridResult Keys()
    {
        if (State.View.Length == 0) return default;
        var result = default(MetadataGridResult);
        var keys = Ui.Input.Keys;
        for (var i = 0; i < keys.Count; i++)
        {
            var stroke = keys[i];
            var modifiers = stroke.Modifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Super);
            switch (modifiers, stroke.Key)
            {
                case (0, KeyboardKey.Space) when State.IsOpen:
                case (0, KeyboardKey.Escape) when State.IsOpen:
                    MetadataGridWidget.CollapseCurrentCell(State);
                    MetadataGridWidget.SelectAllText(State);
                    continue;
                case (0, KeyboardKey.Space) when State.HasCurrentCell && MetadataGrids.GetLines(Grid, State.Selected, State.CurrentColumn).MoreLines > 0:
                    MetadataGridWidget.ExpandCell(State, State.Selected, State.CurrentColumn);
                    MetadataGridWidget.SelectAllText(State);
                    continue;
            }
            if (!State.HasCurrentCell)
                continue;
            if (Ui.IsPressedAt(Shortcuts.SelectAll, i))
                MetadataGridWidget.SelectAllText(State);
            else if (Ui.IsPressedAt(Shortcuts.GoToToken, i))
            {
                var target = MetadataGrids.GetLink(Grid, State.Selected, State.CurrentColumn);
                if (target != null)
                    result = new MetadataGridResult(target, null, false);
            }
        }
        return result;
    }

    private void Rows(TableFrame table, TableColumns columns)
    {
        var rowHeight = State.RowHeight;
        var width = columns.TotalWidth + Ui.Theme.ShellPaddingX * 2;
        var details = MetadataDetailsView.Height(Ui, Grid, State.Details, State.Selected, width);
        MetadataGridWidget.MeasureTallRows(State, rowHeight, details);
        using var body = table.Body(State.View.Length, tall: State.TallRows);
        Arrive(body, rowHeight);
        for (var index = body.First; index < body.End; index++)
        {
            var row = MetadataGridWidget.GetRowAt(State, index);
            var height = State.TallRowsByIndex.GetValueOrDefault(index);
            if (height.Cells <= 0f)
            {
                Row(body, index, row, rowHeight);
                continue;
            }
            using (Ui.Column(new Box { Width = width, Height = height.Cells + height.Details, Gap = 0f }))
            {
                Row(body, index, row, height.Cells);
                if (height.Details > 0f)
                    MetadataDetailsView.Render(Ui, Grid, State.Details, row, height.Details);
            }
        }
        Answer(body);
    }

    private void Arrive(TableBody body, float rowHeight)
    {
        if (body.Viewport <= 0f) return;
        var pending = State.Pending;
        if (pending == null) return;
        State.Pending = null;
        switch (pending)
        {
            case PendingGridScroll.Restore restore:
                ArriveAtRestored(body, rowHeight, restore.Position);
                break;
            case PendingGridScroll.Reveal reveal:
                ArriveAtRevealed(body, rowHeight, reveal.Row);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private void ArriveAtRestored(TableBody body, float rowHeight, GridPosition restore)
    {
        State.Scroll.X = restore.ScrollX;
        body.ScrollTo(restore.TopRow * rowHeight);
        var row = restore.Row;
        if (row != null)
            MetadataGridWidget.SelectCell(State, row.Value, restore.Column ?? MetadataGridState.None);
    }

    private void ArriveAtRevealed(TableBody body, float rowHeight, int row)
    {
        MetadataGridWidget.SelectCell(State, row, State.CurrentColumn);
        var index = MetadataGridWidget.FindIndexOf(State, row);
        if (index < 0) return;
        var top = body.TopOf(index);
        var outOfView = top < body.Offset || top + rowHeight > body.Offset + body.Viewport;
        if (outOfView)
            body.ScrollTo(MathF.Max(0f, top - body.Viewport / 3f));
    }

    private void Row(TableBody body, int index, int row, float height)
    {
        using var cells = body.Row(index, row, height);
        for (var c = 0; c < Grid.Columns.Count; c++)
        {
            ExpandOnPress(cells.CellAt(c), row, c);
            Cell(cells, row, c);
        }
    }

    private void ExpandOnPress(Interaction cell, int row, int column)
    {
        if (!cell.Pressed) return;
        if (!CanExpand(row, column)) return;
        MetadataGridWidget.ExpandCell(State, row, column);
    }

    private bool CanExpand(int row, int column)
    {
        if (MetadataGridWidget.IsOpenAt(State, row, column))
            return false;
        return MetadataGrids.GetLines(Grid, row, column).MoreLines > 0;
    }

    /// <summary>Does what the user asked of the rows this frame: open, copy or show the menu.</summary>
    private void Answer(TableBody body)
    {
        if (body.Activated)
            OpenDefinition(State.Selected, body.InNewTab);
        if (body.CopyRequested)
            Ui.ClipboardText = MetadataGridWidget.GetCopiedText(State);
        if (body.ContextMenuRequested)
            Ui.OpenContextMenu(MenuId, body.ContextMenuAt);
    }

    private void OpenDefinition(int row, bool inNewTab)
    {
        var definition = MetadataGrids.FindDefinition(Grid, row);
        if (definition == null) return;
        _result = new MetadataGridResult(null, definition, inNewTab);
    }

    private void Cell(TableRow cells, int row, int c)
    {
        var theme = Ui.Theme;
        var column = Grid.Columns[c];
        var cellId = cells.CellAt(c).Id;
        var target = column.IsToken ? MetadataGrids.GetLink(Grid, row, c) : null;
        var expanded = MetadataGridWidget.IsOpenAt(State, row, c);
        uint? color = theme.Text;
        UiIcon? link = null;
        if (target != null)
        {
            color = null;
            link = new UiIcon(Ui.State.Icons.LinkToken);
        }
        float? size = null;
        string? family = null;
        if (IsMono(column))
        {
            size = theme.FontSize;
            family = MonoFonts.Code;
        }
        string? more = null;
        if (!expanded)
            more = MoreLines(row, c, cellId);
        var followed = cells.TextCell(c, MetadataGridWidget.GetShownTextAt(State, row, c), color, size, family, link: link, linkTooltip: Strings.MetadataGoToToken, suffix: more,
            expanded: expanded);
        if (followed)
            _result = new MetadataGridResult(target, null, false);
        Tooltip(row, c, cellId);
    }
    /// <summary>How many lines a closed multi-line cell hides, as its suffix; null for a single line.</summary>
    private string? MoreLines(int row, int column, int cellId)
    {
        var more = MetadataGrids.GetLines(Grid, row, column).MoreLines;
        if (more == 0) return null;
        var t = Strings;
        using (Ui.Id(cellId))
            return Ui.Memo("more-lines", (more, t), static a => a.t.MetadataMoreLines(a.more));
    }

    private void Tooltip(int row, int column, int cellId)
    {
        if (!Ui.IsHovered(cellId)) return;
        var content = MetadataGrids.GetTooltip(Grid, row, column);
        if (content == null) return;
        using var tooltip = Ui.TooltipContent(cellId);
        if (!tooltip.IsOpen) return;
        MetadataTooltipView.Render(Ui, content);
    }

    private MetadataGridResult? Menu()
    {
        using var menu = Ui.ContextMenu(MenuId);
        if (!menu.IsOpen) return null;
        if (State.Selected < 0) return null;
        if (State.Selected >= Grid.RowCount) return null;
        var row = State.Selected;
        var column = State.CurrentColumn;
        MetadataGridResult? result = null;
        if (Ui.MenuItem(Strings.CodeCopy, Shortcuts.Copy, enabled: column >= 0, id: "copy"))
            Ui.ClipboardText = MetadataGridWidget.GetCellText(State);
        if (Ui.MenuItem(Strings.MetadataCopyRow, id: "copy-row"))
            Ui.ClipboardText = MetadataGridWidget.GetRowText(State, row);
        var target = column >= 0 ? MetadataGrids.GetLink(Grid, row, column) : null;
        var definition = MetadataGrids.FindDefinition(Grid, row);
        if (target == null && definition == null) return null;
        Ui.MenuSeparator();
        if (target != null && Ui.MenuItem(Strings.MetadataGoToToken, Shortcuts.GoToToken, id: "go-to-token"))
            result = new MetadataGridResult(target, null, false);
        if (definition != null)
        {
            if (Ui.MenuItem(Strings.CodeGoToDefinition, Shortcuts.Enter, id: "go-to-definition"))
                result = new MetadataGridResult(null, definition, false);
            if (Ui.MenuItem(Strings.CodeOpenInNewTab, id: "open-in-new-tab"))
                result = new MetadataGridResult(null, definition, true);
        }
        return result;
    }
}
