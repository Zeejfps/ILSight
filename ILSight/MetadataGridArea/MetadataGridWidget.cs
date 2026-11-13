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

        return new MetadataColumnFilter.ByFlags { Flags = flags.GetFilter(state.Grid, column) };
    }

    private static bool IsFlagsColumn(MetadataGridState state, int column)
    {
        return state.Grid.Columns[column].Flags != null;
    }

    /// <summary>Asks <paramref name="views"/> for the rows sorted and filtered again when a filter or the sort
    /// changed: a wait, which the frame edge starts off the screen's thread, the view showing the rows it has until
    /// they land (<see cref="MetadataGridState.ShowArranged"/>). The table's own rows, unsorted and unfiltered, read no cell, and show
    /// at once. The grid of a closed assembly (<paramref name="isClosed"/>) asks for nothing: a sort or filter stays
    /// unavailable, the view saying why.</summary>
    internal static void UpdateView(MetadataGridState state, MetadataFilters flags, MetadataGridViews views, bool isClosed)
    {
        var key = ComputeViewKey(state, flags);
        if (!state.TryAskView(key))
        {
            return;
        }

        if (state.SortColumn == MetadataGridState.None && !HasActiveFilter(state))
        {
            views.Forget(state);
            state.ShowUnarranged(key, Enumerable.Range(0, state.Grid.RowCount).ToArray());
            return;
        }

        if (isClosed)
        {
            views.Forget(state);
            return;
        }

        var order = state.FindReusableOrder();
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

    /// <summary>Makes <paramref name="row"/>'s cell in <paramref name="column"/> the current one
    /// with all its text selected, where the view shows the row.</summary>
    internal static void SelectCell(MetadataGridState state, int row, int column)
    {
        var index = state.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        state.Cell.Select(index, state.RowCount, column, reveal: false);
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

        if (state.IsOpenAt(row, column))
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
            var edit = ui.InlineEdit("filter", ref text, state.TryTakeBeginEdit(), Sizing.Grow());
            filter.Text = edit == InlineEditResult.Cancelled ? "" : text;
            if (edit != InlineEditResult.Editing)
            {
                state.StopEditingFilter();
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
            state.StartEditingFilter(column);
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

        if (!heading.State.Flyout.IsOpenFor(column))
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
            _ => state.Flyout.Toggle(column));
    }

    private static void ClickTextFunnel(MetadataGridState state, MetadataColumnFilter.ByText text, int column)
    {
        if (text.Text.Length > 0)
        {
            text.Text = "";
            state.StopEditingFilter();
            return;
        }

        state.StartEditingFilter(column);
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
