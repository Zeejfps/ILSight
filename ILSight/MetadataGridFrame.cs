using System.Diagnostics;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core.Metadata;
using ILSight.Localization;
using ZGF.KeyboardModule;

namespace ILSight;

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
        {
            _result = Keys();
        }

        Rows(table, columns);
        var picked = Menu();
        if (picked is not null)
        {
            _result = picked.Value;
        }

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
        {
            width = MathF.Max(width, Ui.MeasureText(MetadataGrids.GetText(Grid, row, c), size, family: family));
        }

        return width;
    }

    private static bool IsMono(MetadataColumn column)
    {
        return column.IsNumeric || column.IsToken;
    }

    private void Header(TableFrame table)
    {
        var icons = Ui.State.Icons;
        var sortGlyph = State.Descending ? icons.SortDescending : icons.SortAscending;
        var sort = new TableSort(State.SortColumn, new UiIcon(sortGlyph));
        var heading = new MetadataGridHeading { Grid = Grid, State = State, Flags = Flags, Strings = Strings, Icons = icons, ListId = ListId };
        var clicked = table.Header(State.Headers, sort, MetadataGridWidget.RenderHeading, heading);
        if (clicked < 0)
        {
            return;
        }

        if (clicked == State.EditingColumn)
        {
            return;
        }

        MetadataGridWidget.SortBy(State, clicked);
    }

    private void FlagsFlyout()
    {
        if (State.FlagsColumn < 0)
        {
            return;
        }

        var theme = Ui.Theme;
        var filter = MetadataGridWidget.GetFilter(Flags, Grid, State.FlagsColumn);
        var width = FlagsFilterView.ContentWidth + theme.TooltipPaddingX * 2 + theme.ScrollBarHitWidth;
        var funnelId = MetadataGridWidget.GetFunnelId(ListId, State.FlagsColumn);
        using var flyout = Ui.Flyout(HashCode.Combine(ListId, FlyoutSalt), ref State.FlagsOpen, funnelId,
            new Box { Width = Sizing.Fit(FlyoutMinWidth, width), Gap = theme.Gap });
        if (!flyout.IsOpen)
        {
            return;
        }

        FlagsFilterView.Render(Ui, filter);
    }

    private MetadataGridResult Keys()
    {
        if (State.View.Length == 0)
        {
            return default;
        }

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
            {
                continue;
            }

            if (Ui.IsPressedAt(Shortcuts.SelectAll, i))
            {
                MetadataGridWidget.SelectAllText(State);
            }
            else if (Ui.IsPressedAt(Shortcuts.GoToToken, i))
            {
                var target = MetadataGrids.FindLink(Grid, State.Selected, State.CurrentColumn);
                if (target is not null)
                {
                    result = new MetadataGridResult(target, null, false);
                }
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
                {
                    MetadataDetailsView.Render(Ui, Grid, State.Details, row, height.Details);
                }
            }
        }
        Answer(body);
    }

    private void Arrive(TableBody body, float rowHeight)
    {
        if (body.Viewport <= 0f)
        {
            return;
        }

        var pending = State.Pending;
        if (pending is null)
        {
            return;
        }

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
        if (row is not null)
        {
            MetadataGridWidget.SelectCell(State, row.Value, restore.Column ?? MetadataGridState.None);
        }
    }

    private void ArriveAtRevealed(TableBody body, float rowHeight, int row)
    {
        MetadataGridWidget.SelectCell(State, row, State.CurrentColumn);
        var index = MetadataGridWidget.GetIndexOf(State, row);
        if (index < 0)
        {
            return;
        }

        var top = body.TopOf(index);
        var outOfView = top < body.Offset || top + rowHeight > body.Offset + body.Viewport;
        if (outOfView)
        {
            body.ScrollTo(MathF.Max(0f, top - body.Viewport / 3f));
        }
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
        if (!cell.Pressed)
        {
            return;
        }

        if (!CanExpand(row, column))
        {
            return;
        }

        MetadataGridWidget.ExpandCell(State, row, column);
    }

    private bool CanExpand(int row, int column)
    {
        if (MetadataGridWidget.IsOpenAt(State, row, column))
        {
            return false;
        }

        return MetadataGrids.GetLines(Grid, row, column).MoreLines > 0;
    }

    /// <summary>Does what the user asked of the rows this frame: open, copy or show the menu.</summary>
    private void Answer(TableBody body)
    {
        if (body.Activated)
        {
            OpenDefinition(State.Selected, body.InNewTab);
        }

        if (body.CopyRequested)
        {
            Ui.ClipboardText = MetadataGridWidget.GetCopiedText(State);
        }

        if (body.ContextMenuRequested)
        {
            Ui.OpenContextMenu(MenuId, body.ContextMenuAt);
        }
    }

    private void OpenDefinition(int row, bool inNewTab)
    {
        var definition = MetadataGrids.FindDefinition(Grid, row);
        if (definition == null)
        {
            return;
        }

        _result = new MetadataGridResult(null, definition, inNewTab);
    }

    private void Cell(TableRow cells, int row, int c)
    {
        var theme = Ui.Theme;
        var column = Grid.Columns[c];
        var cellId = cells.CellAt(c).Id;
        var target = column.IsToken ? MetadataGrids.FindLink(Grid, row, c) : null;
        var expanded = MetadataGridWidget.IsOpenAt(State, row, c);
        uint? color = theme.Text;
        UiIcon? link = null;
        if (target is not null)
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
        {
            more = MoreLines(row, c, cellId);
        }

        var followed = cells.TextCell(c, MetadataGridWidget.GetShownTextAt(State, row, c), color, size, family, link: link, linkTooltip: Strings.MetadataGoToToken, suffix: more,
            expanded: expanded);
        if (followed)
        {
            _result = new MetadataGridResult(target, null, false);
        }

        Tooltip(row, c, cellId);
    }
    /// <summary>How many lines a closed multi-line cell hides, as its suffix; null for a single line.</summary>
    private string? MoreLines(int row, int column, int cellId)
    {
        var more = MetadataGrids.GetLines(Grid, row, column).MoreLines;
        if (more == 0)
        {
            return null;
        }

        var t = Strings;
        using (Ui.Id(cellId))
        {
            return Ui.Memo("more-lines", (more, t), static a => a.t.MetadataMoreLines(a.more));
        }
    }

    private void Tooltip(int row, int column, int cellId)
    {
        if (!Ui.IsHovered(cellId))
        {
            return;
        }

        var content = MetadataGrids.FindTooltip(Grid, row, column);
        if (content == null)
        {
            return;
        }

        using var tooltip = Ui.TooltipContent(cellId);
        if (!tooltip.IsOpen)
        {
            return;
        }

        MetadataTooltipView.Render(Ui, content);
    }

    private MetadataGridResult? Menu()
    {
        using var menu = Ui.ContextMenu(MenuId);
        if (!menu.IsOpen)
        {
            return null;
        }

        if (State.Selected < 0)
        {
            return null;
        }

        if (State.Selected >= Grid.RowCount)
        {
            return null;
        }

        var row = State.Selected;
        var column = State.CurrentColumn;
        MetadataGridResult? result = null;
        if (Ui.MenuItem(Strings.CodeCopy, Shortcuts.Copy, enabled: column >= 0, id: "copy"))
        {
            Ui.ClipboardText = MetadataGridWidget.GetCellText(State);
        }

        if (Ui.MenuItem(Strings.MetadataCopyRow, id: "copy-row"))
        {
            Ui.ClipboardText = MetadataGridWidget.GetRowText(State, row);
        }

        var target = column >= 0 ? MetadataGrids.FindLink(Grid, row, column) : null;
        var definition = MetadataGrids.FindDefinition(Grid, row);
        if (target is null && definition == null)
        {
            return null;
        }

        Ui.MenuSeparator();
        if (target is not null && Ui.MenuItem(Strings.MetadataGoToToken, Shortcuts.GoToToken, id: "go-to-token"))
        {
            result = new MetadataGridResult(target, null, false);
        }

        if (definition != null)
        {
            if (Ui.MenuItem(Strings.CodeGoToDefinition, Shortcuts.Enter, id: "go-to-definition"))
            {
                result = new MetadataGridResult(null, definition, false);
            }

            if (Ui.MenuItem(Strings.CodeOpenInNewTab, id: "open-in-new-tab"))
            {
                result = new MetadataGridResult(null, definition, true);
            }
        }
        return result;
    }
}
