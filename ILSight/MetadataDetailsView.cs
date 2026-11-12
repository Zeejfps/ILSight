using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.CodeView;

namespace ILSight;

/// <summary>What is decoded from a metadata grid's selected row, shown under it: a text in a code view,
/// or a table.</summary>
internal static class MetadataDetailsView
{
    private const int MaxTextLines = 12;
    private const float TextPaddingX = 24f;
    private const float MaxTableHeight = 250f;

    /// <summary>How tall <paramref name="row"/>'s details are in a grid <paramref name="width"/> wide;
    /// 0 when it has none.</summary>
    public static float Height(Ui<UiState> ui, MetadataGrid grid, DetailTexts texts, int row, float width)
    {
        if (row < 0)
        {
            return 0f;
        }

        if (!grid.HasDetails)
        {
            return 0f;
        }

        var details = MetadataGrids.FindDetails(grid, row);
        if (details is null)
        {
            return 0f;
        }

        var padding = ui.Theme.GapTight * 2;
        return details.Match(
            (Ui: ui, Texts: texts, Row: row, Width: width, Padding: padding),
            static (at, text) => TextHeight(at.Ui, MeasureText(at.Texts, at.Row, text.Content), at.Width - Left(at.Ui.Theme) - at.Ui.Theme.ShellPaddingX) + at.Padding,
            static (at, table) => MathF.Min(MaxTableHeight, (table.Rows.Count + 1) * at.Ui.Theme.RowHeight + at.Padding));
    }

    /// <summary>How big <paramref name="row"/>'s <paramref name="text"/> is, measured once.</summary>
    private static TextSize MeasureText(DetailTexts texts, int row, string text)
    {
        if (texts.Sizes.TryGetValue(row, out var size))
        {
            return size;
        }

        size = MeasureText(text);
        texts.Sizes[row] = size;
        return size;
    }

    /// <summary>How many lines <paramref name="text"/> has, and how many characters its longest one.</summary>
    private static TextSize MeasureText(string text)
    {
        var lines = text.Split('\n');
        var longest = lines.Max(static l => l.TrimEnd('\r').Length);
        return new TextSize(lines.Length, longest);
    }

    /// <summary>How tall a text is in a code view <paramref name="width"/> wide: a code line per
    /// line, up to <see cref="MaxTextLines"/> before it scrolls, plus the horizontal scrollbar's room
    /// when its longest line is wider.</summary>
    private static float TextHeight(Ui<UiState> ui, TextSize size, float width)
    {
        var theme = ui.Theme;
        var cell = ui.MeasureCell(theme.MonoFontSize, family: MonoFonts.Code);
        var lineHeight = MathF.Max(theme.CodeLineHeight, MathF.Ceiling(cell.Height));
        var overflows = TextPaddingX + size.Longest * cell.Advance > width;
        var bar = overflows ? theme.ScrollBarWidth + theme.ScrollBarInset * 2 : 0f;
        return Math.Min(size.Lines, MaxTextLines) * lineHeight + bar;
    }

    private static float Left(UiTheme theme)
    {
        return theme.ShellPaddingX + theme.IconSize;
    }

    public static void Render(Ui<UiState> ui, MetadataGrid grid, DetailTexts texts, int row, float height)
    {
        var theme = ui.Theme;
        using var box = ui.Column(new Box
        {
            Width = Sizing.Grow(),
            Height = height,
            Gap = 0f,
            Padding = new Spacing(Left(theme), theme.GapTight, theme.ShellPaddingX, theme.GapTight),
            Background = theme.Surface,
        });
        using var scope = ui.Id(row);
        var details = MetadataGrids.FindDetails(grid, row);
        details?.Switch(
            (Ui: ui, Grid: grid, Texts: texts, Row: row),
            static (at, text) => Text(at.Ui, at.Grid, at.Texts, at.Row, text),
            static (at, table) => Table(at.Ui, table));
    }

    /// <summary><paramref name="row"/>'s <paramref name="text"/> as a document, made once.</summary>
    private static DetailText GetText(DetailTexts texts, int row, string title, string text)
    {
        if (texts.Texts.TryGetValue(row, out var shown))
        {
            return shown;
        }

        var document = Documents.CreateFromText(title, text);
        shown = new DetailText(document, new CodeViewState());
        texts.Texts[row] = shown;
        return shown;
    }

    private static void Text(Ui<UiState> ui, MetadataGrid grid, DetailTexts texts, int row, MetadataRowDetails.Text text)
    {
        var shown = GetText(texts, row, grid.Title, text.Content);
        CodeViewWidget.RenderCodeView(ui, "details", shown.Document, shown.View, gutter: false);
    }

    private static void Table(Ui<UiState> ui, MetadataRowDetails.Table table)
    {
        var theme = ui.Theme;
        var share = 1f / table.Headers.Count;
        using (ui.Row(new Box { Height = theme.RowHeight, Gap = theme.GapTight, AlignY = Align.Center }))
        {
            var headers = table.Headers;
            for (var headerIndex = 0; headerIndex < headers.Count; headerIndex++)
            {
                var header = headers[headerIndex];
                ui.Text(header, new Box { Width = Sizing.Percent(share) }, color: theme.TextMuted, size: theme.FontSizeSmall, overflow: TextOverflow.Ellipsis);
            }
        }
        using (ui.Scroll("details", new Box { Height = Sizing.Grow(), Gap = 0f }))
        {
            var rows = table.Rows;
            for (var cellsIndex = 0; cellsIndex < rows.Count; cellsIndex++)
            {
                var cells = rows[cellsIndex];
                TableRow(ui, cells, share);
            }
        }
    }

    private static void TableRow(Ui<UiState> ui, IReadOnlyList<string> cells, float share)
    {
        var theme = ui.Theme;
        using (ui.Row(new Box { Height = theme.RowHeight, Gap = theme.GapTight, AlignY = Align.Center }))
        {
            for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                var cell = cells[cellIndex];
                ui.Text(cell, new Box { Width = Sizing.Percent(share) }, overflow: TextOverflow.Ellipsis);
            }
        }
    }
}
