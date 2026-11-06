using System.Diagnostics;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.Localization;
using ZGF.KeyboardModule;

namespace ILSight;

/// <summary>What the user asked of a <see cref="MetadataGridWidget.MetadataGridView"/> this frame.</summary>
/// <param name="Target">The table row a token cell leads to.</param>
/// <param name="Definition">What a row stands for in the code, to decompile.</param>
/// <param name="InNewTab">Whether to show either in a new tab.</param>
public readonly record struct MetadataGridResult(MetadataAddress? Target, CodeReference.Navigable? Definition, bool InNewTab);

public static class MetadataGridWidget
{
    private const int FunnelSalt = 0x4D444746;
    private const int FilterTextSalt = 0x4D444754;

    public static MetadataGridResult MetadataGridView(this Ui<UiState> ui, string id, MetadataGridState state, MetadataFilters flags)
    {
        var frame = new MetadataGridFrame(ui, id, state, flags);
        return frame.Render();
    }

    /// <summary>The filter of <paramref name="column"/>, a column of flags, as last left.</summary>
    public static FilterState FindFilter(MetadataFilters filters, MetadataGrid grid, int column)
    {
        var key = GetKey(grid, column);
        if (filters.States.TryGetValue(key, out var state)) return state;
        var schema = grid.Columns[column].Flags;
        if (schema == null)
            throw new ArgumentException($"{key} is not a flags column.", nameof(column));
        state = new FilterState(schema);
        if (filters.Saved.TryGetValue(key, out var saved))
            state.Restore(saved);
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
            var saved = state.Save();
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
        var index = state.IndexOf(row);
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
        var filter = heading.State.FilterOf(column, heading.Flags);
        var textId = HashCode.Combine(heading.ListId, FilterTextSalt, column);
        if (heading.State.EditingColumn == column)
            EditFilterText(ui, heading.State, filter, textId);
        else if (filter.Text.Length > 0)
            DrawFilterText(ui, heading.State, filter, textId, column);
        else
            DrawColumnName(ui, heading.Grid.Columns[column].Name, hovered);
        DrawFunnel(ui, in heading, filter, column);
    }

    private static void EditFilterText(Ui ui, MetadataGridState state, MetadataColumnFilter filter, int textId)
    {
        var text = filter.Text;
        using (ui.Id(textId))
        {
            var edit = ui.InlineEdit("filter", ref text, state.BeginEdit, Sizing.Grow());
            state.BeginEdit = false;
            filter.Text = edit == InlineEditResult.Cancelled ? "" : text;
            if (edit != InlineEditResult.Editing)
                state.StopEditingFilter();
        }
    }

    private static void DrawFilterText(Ui ui, MetadataGridState state, MetadataColumnFilter filter, int textId, int column)
    {
        var theme = ui.Theme;
        var shown = ui.Interact(textId);
        if (shown.Hovered)
            ui.SetCursor(MouseCursor.Text);
        if (shown.Clicked)
            state.StartEditingFilter(column);
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
        if (!heading.State.IsFlagsFlyoutOpenFor(column))
            ui.Tooltip(funnel.Id, GetFunnelTooltip(ui, heading.Strings, funnel.Id, heading.Grid.Columns[column].Name));
    }

    private static string GetFunnelTooltip(Ui ui, Strings t, int funnelId, string name)
    {
        using (ui.Id(funnelId))
            return ui.Memo("tooltip", (name, t), static a => a.t.MetadataFilter(a.name));
    }

    private static void ClickFunnel(MetadataGridState state, MetadataColumnFilter filter, int column)
    {
        if (filter.Flags != null)
        {
            state.ToggleFlagsFlyout(column);
            return;
        }
        if (filter.Text.Length > 0)
        {
            filter.Text = "";
            state.StopEditingFilter();
            return;
        }
        state.StartEditingFilter(column);
    }

    private static IconGlyph GetFunnelGlyph(AppIcons icons, MetadataColumnFilter filter)
    {
        if (filter.Flags != null)
            return icons.MetadataFilter;
        if (filter.Text.Length > 0)
            return icons.MetadataClearFilter;
        return icons.MetadataFilter;
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

/// <summary>One frame of a <see cref="MetadataGridWidget.MetadataGridView"/>: what every step of
/// drawing it shares, kept on the stack.</summary>
internal ref struct MetadataGridFrame
{
    private const int AutoFitRows = 200;
    private const float MaxAutoWidth = 480f;
    private const float FlyoutMinWidth = 320f;
    private const int FlyoutSalt = 0x4D44464C;
    private const int MenuSalt = 0x4D44474D;

    private readonly Ui<UiState> _ui;
    private readonly MetadataGrid _grid;
    private readonly MetadataGridState _state;
    private readonly MetadataFilters _flags;
    private readonly Strings _t;
    private readonly int _listId;
    private readonly int _menuId;
    private bool _focused;
    private MetadataGridResult _result;

    public MetadataGridFrame(Ui<UiState> ui, string id, MetadataGridState state, MetadataFilters flags)
    {
        _ui = ui;
        _grid = state.Grid;
        _state = state;
        _flags = flags;
        _t = ui.State.Strings;
        _listId = ui.GetId(id);
        _menuId = HashCode.Combine(_listId, MenuSalt);
    }

    public MetadataGridResult Render()
    {
        var columns = _state.Columns ??= Measure();
        _state.RowHeight = _ui.Theme.RowHeight;
        _state.UpdateView(_flags);

        using var pane = _ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f, Background = _ui.Theme.PaneBackground });
        var table = _ui.Table(_listId, columns, _state.Cell, _state.Scroll);
        Header(table);
        FlagsFlyout();
        _state.UpdateView(_flags);

        _focused = table.Focused;
        table.TakeKeys(_state.View.Length);
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
        var theme = _ui.Theme;
        var padding = theme.GapTight * 2;
        var widths = new float[_grid.Columns.Count];
        var rows = Math.Min(_grid.RowCount, AutoFitRows);
        for (var c = 0; c < widths.Length; c++)
        {
            var width = MeasureColumn(c, rows);
            widths[c] = MathF.Ceiling(MathF.Min(MaxAutoWidth, width + padding + theme.GapTight));
        }
        return TableColumns.Sized(widths);
    }

    private float MeasureColumn(int c, int rows)
    {
        var theme = _ui.Theme;
        var column = _grid.Columns[c];
        float? size = null;
        string? family = null;
        if (IsMono(column))
        {
            size = theme.FontSize;
            family = MonoFonts.Code;
        }
        var width = _ui.MeasureText(column.Name, theme.FontSizeSmall) + theme.IconSize * 2 + theme.GapTight * 2;
        for (var row = 0; row < rows; row++)
            width = MathF.Max(width, _ui.MeasureText(_grid.Text(row, c), size, family: family));
        return width;
    }

    private static bool IsMono(MetadataColumn column) => column.IsNumeric || column.IsToken;

    private void Header(TableFrame table)
    {
        var icons = _ui.State.Icons;
        var sortGlyph = _state.Descending ? icons.SortDescending : icons.SortAscending;
        var sort = new TableSort(_state.SortColumn, new UiIcon(sortGlyph));
        var heading = new MetadataGridHeading(_grid, _state, _flags, _t, icons, _listId);
        var clicked = table.Header(_state.Headers, sort, MetadataGridWidget.RenderHeading, heading);
        if (clicked < 0) return;
        if (clicked == _state.EditingColumn) return;
        _state.SortBy(clicked);
    }

    private void FlagsFlyout()
    {
        if (_state.FlagsColumn < 0) return;
        var theme = _ui.Theme;
        var filter = MetadataGridWidget.FindFilter(_flags, _grid, _state.FlagsColumn);
        var width = FlagsFilterView.ContentWidth + theme.TooltipPaddingX * 2 + theme.ScrollBarHitWidth;
        var funnelId = MetadataGridWidget.GetFunnelId(_listId, _state.FlagsColumn);
        using var flyout = _ui.Flyout(HashCode.Combine(_listId, FlyoutSalt), ref _state.FlagsOpen, funnelId,
            new Box { Width = Sizing.Fit(FlyoutMinWidth, width), Gap = theme.Gap });
        if (!flyout.IsOpen) return;
        FlagsFilterView.Render(_ui, filter);
    }

    private MetadataGridResult Keys()
    {
        if (_state.View.Length == 0) return default;
        var result = default(MetadataGridResult);
        var keys = _ui.Input.Keys;
        for (var i = 0; i < keys.Count; i++)
        {
            var stroke = keys[i];
            var modifiers = stroke.Modifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Super);
            switch (modifiers, stroke.Key)
            {
                case (0, KeyboardKey.Space) when _state.IsOpen:
                case (0, KeyboardKey.Escape) when _state.IsOpen:
                    _state.CollapseCurrent();
                    _state.SelectAllText();
                    continue;
                case (0, KeyboardKey.Space) when _state.HasCurrentCell && _grid.Lines(_state.Selected, _state.CurrentColumn).MoreLines > 0:
                    _state.Expand(_state.Selected, _state.CurrentColumn);
                    _state.SelectAllText();
                    continue;
            }
            if (!_state.HasCurrentCell)
                continue;
            if (_ui.IsPressedAt(Shortcuts.SelectAll, i))
                _state.SelectAllText();
            else if (_ui.IsPressedAt(Shortcuts.GoToToken, i))
            {
                var target = _grid.Link(_state.Selected, _state.CurrentColumn);
                if (target != null)
                    result = new MetadataGridResult(target, null, false);
            }
        }
        return result;
    }

    private void Rows(TableFrame table, TableColumns columns)
    {
        var rowHeight = _state.RowHeight;
        var width = columns.TotalWidth + _ui.Theme.ShellPaddingX * 2;
        var details = MetadataDetailsView.Height(_ui, _grid, _state.Details, _state.Selected, width);
        MetadataGridWidget.MeasureTallRows(_state, rowHeight, details);
        using var body = table.Body(_state.View.Length, tall: _state.TallRows);
        Arrive(body, rowHeight);
        for (var index = body.First; index < body.End; index++)
        {
            var row = _state.RowAt(index);
            var height = _state.TallRowsByIndex.GetValueOrDefault(index);
            if (height.Cells <= 0f)
            {
                Row(body, index, row, rowHeight);
                continue;
            }
            using (_ui.Column(new Box { Width = width, Height = height.Cells + height.Details, Gap = 0f }))
            {
                Row(body, index, row, height.Cells);
                if (height.Details > 0f)
                    MetadataDetailsView.Render(_ui, _grid, _state.Details, row, height.Details);
            }
        }
        Answer(body);
    }

    private void Arrive(TableBody body, float rowHeight)
    {
        if (body.Viewport <= 0f) return;
        var pending = _state.Pending;
        if (pending == null) return;
        _state.Pending = null;
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
        _state.Scroll.X = restore.ScrollX;
        body.ScrollTo(restore.TopRow * rowHeight);
        var row = restore.Row;
        if (row != null)
            _state.SelectCell(row.Value, restore.Column ?? MetadataGridState.None);
    }

    private void ArriveAtRevealed(TableBody body, float rowHeight, int row)
    {
        _state.SelectCell(row, _state.CurrentColumn);
        var index = _state.IndexOf(row);
        if (index < 0) return;
        var top = body.TopOf(index);
        var outOfView = top < body.Offset || top + rowHeight > body.Offset + body.Viewport;
        if (outOfView)
            body.ScrollTo(MathF.Max(0f, top - body.Viewport / 3f));
    }

    private void Row(TableBody body, int index, int row, float height)
    {
        using var cells = body.Row(index, row, height);
        for (var c = 0; c < _grid.Columns.Count; c++)
        {
            ExpandOnPress(cells.CellAt(c), row, c);
            Cell(cells, row, c);
        }
    }

    private void ExpandOnPress(Interaction cell, int row, int column)
    {
        if (!cell.Pressed) return;
        if (!CanExpand(row, column)) return;
        _state.Expand(row, column);
    }

    private bool CanExpand(int row, int column)
    {
        if (_state.IsOpenAt(row, column))
            return false;
        return _grid.Lines(row, column).MoreLines > 0;
    }

    /// <summary>Does what the user asked of the rows this frame: open, copy or show the menu.</summary>
    private void Answer(TableBody body)
    {
        if (body.Activated)
            OpenDefinition(_state.Selected, body.InNewTab);
        if (body.CopyRequested)
            _ui.ClipboardText = _state.CopiedText();
        if (body.ContextMenuRequested)
            _ui.OpenContextMenu(_menuId, body.ContextMenuAt);
    }

    private void OpenDefinition(int row, bool inNewTab)
    {
        var definition = _grid.Definition(row);
        if (definition == null) return;
        _result = new MetadataGridResult(null, definition, inNewTab);
    }

    private void Cell(TableRow cells, int row, int c)
    {
        var theme = _ui.Theme;
        var column = _grid.Columns[c];
        var cellId = cells.CellAt(c).Id;
        var target = column.IsToken ? _grid.Link(row, c) : null;
        var expanded = _state.IsOpenAt(row, c);
        uint? color = theme.Text;
        UiIcon? link = null;
        if (target != null)
        {
            color = null;
            link = new UiIcon(_ui.State.Icons.LinkToken);
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
        var followed = cells.TextCell(c, _state.ShownAt(row, c), color, size, family, link: link, linkTooltip: _t.MetadataGoToToken, suffix: more,
            expanded: expanded);
        if (followed)
            _result = new MetadataGridResult(target, null, false);
        Tooltip(row, c, cellId);
    }
    /// <summary>How many lines a closed multi-line cell hides, as its suffix; null for a single line.</summary>
    private string? MoreLines(int row, int column, int cellId)
    {
        var more = _grid.Lines(row, column).MoreLines;
        if (more == 0) return null;
        var t = _t;
        using (_ui.Id(cellId))
            return _ui.Memo("more-lines", (more, t), static a => a.t.MetadataMoreLines(a.more));
    }

    private void Tooltip(int row, int column, int cellId)
    {
        if (!_ui.IsHovered(cellId)) return;
        var content = _grid.Tooltip(row, column);
        if (content == null) return;
        using var tooltip = _ui.TooltipContent(cellId);
        if (!tooltip.IsOpen) return;
        MetadataTooltipView.Render(_ui, content);
    }

    private MetadataGridResult? Menu()
    {
        using var menu = _ui.ContextMenu(_menuId);
        if (!menu.IsOpen) return null;
        if (_state.Selected < 0) return null;
        if (_state.Selected >= _grid.RowCount) return null;
        var row = _state.Selected;
        var column = _state.CurrentColumn;
        MetadataGridResult? result = null;
        if (_ui.MenuItem(_t.CodeCopy, Shortcuts.Copy, enabled: column >= 0, id: "copy"))
            _ui.ClipboardText = _state.CellText();
        if (_ui.MenuItem(_t.MetadataCopyRow, id: "copy-row"))
            _ui.ClipboardText = _state.RowText(row);
        var target = column >= 0 ? _grid.Link(row, column) : null;
        var definition = _grid.Definition(row);
        if (target == null && definition == null) return null;
        _ui.MenuSeparator();
        if (target != null && _ui.MenuItem(_t.MetadataGoToToken, Shortcuts.GoToToken, id: "go-to-token"))
            result = new MetadataGridResult(target, null, false);
        if (definition != null)
        {
            if (_ui.MenuItem(_t.CodeGoToDefinition, Shortcuts.Enter, id: "go-to-definition"))
                result = new MetadataGridResult(null, definition, false);
            if (_ui.MenuItem(_t.CodeOpenInNewTab, id: "open-in-new-tab"))
                result = new MetadataGridResult(null, definition, true);
        }
        return result;
    }
}
