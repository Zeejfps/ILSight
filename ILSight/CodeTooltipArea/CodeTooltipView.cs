using System.Runtime.CompilerServices;
using ZGF.Gui;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CodeViewArea;

namespace ILSight.CodeTooltipArea;

/// <summary>What hovering a type or member in code shows: its declaration highlighted as code in a
/// band of its own, on one line when it fits and else with a parameter per line, over the type or
/// namespace it is in and its <c>where</c> clauses; then a rule and its documentation, a heading over each kind of section
/// after the summary. Space between sections is wider than within them, so each reads as a group.
/// Parameters line up in a column beside their descriptions, an exception's description sits
/// under it, list items hang from their markers, and code blocks are set in a box.</summary>
internal static class CodeTooltipView
{
    public const float PaddingX = 12f;
    private const float MaxWidth = 560f;
    private const float MaxHeight = 480f;
    private const float SignaturePaddingY = 8f;
    private const float ContextGap = 2f;
    private const float ParameterIndent = 16f;
    private const float ConstraintGap = 6f;
    private const float DocumentationPaddingY = 10f;
    private const float SectionGap = 11f;
    private const float HeadingGap = 4f;
    private const float EntryGap = 4f;
    private const float ParagraphGap = 6f;
    private const float ItemGap = 3f;
    private const float ColumnGap = 12f;
    private const float MaxLabelWidth = 140f;
    private const float MarkerWidth = 16f;
    private const float MarkerGap = 4f;
    private const float ExceptionIndent = 16f;
    private const float ExceptionGap = 2f;
    private const float CodeBlockPadding = 8f;
    private const float CodeBlockRadius = 4f;
    private const float CodeChipMix = 0.06f;
    private const float CodeBlockMix = 0.3f;
    private const float HeadingMix = 0.2f;

    /// <summary>The popup the tooltip is shown in: unpadded, as the tooltip pads its own parts,
    /// starting where the hovered word starts, and interactive, so its links can be clicked and
    /// long documentation scrolled.</summary>
    public static readonly TooltipLook Look = new(MaxWidth, new Spacing(0f, 0f), Align.Start, Interactive: true, MaxHeight: MaxHeight);

    /// <summary>Records what <paramref name="shown"/> shows: the tooltip, or that the module the type or member is in
    /// could not be read, and why; returns the link clicked in it this frame, if one was.</summary>
    public static RichTextClick Render(Ui<UiState> ui, Lookup<CodeTooltip> shown)
    {
        return shown.Match<Ui<UiState>, RichTextClick>(ui,
            static (u, found) => Render(u, found.Value),
            static (_, _) => default,
            static (u, unreadable) => RenderUnreadable(u, unreadable.Module));
    }

    private static RichTextClick RenderUnreadable(Ui<UiState> ui, UnreadableModule unreadable)
    {
        using (ui.Column(new Box { Padding = new Spacing(PaddingX, SignaturePaddingY), Gap = 0f }))
        {
            var text = ui.State.Strings.CodeTooltipUnreadable(unreadable.Module.FilePath.FileName, unreadable.Why.Message);
            ui.Text(text, color: ui.Theme.Warning, wrap: true);
        }
        return default;
    }

    /// <summary>Records that <paramref name="assembly"/>, the assembly the hovered type or member is in, was closed, where
    /// the tooltip's declaration would be: the code of a closed assembly looks nothing up.</summary>
    public static void RenderClosed(Ui<UiState> ui, string assembly)
    {
        using (ui.Column(new Box { Padding = new Spacing(PaddingX, SignaturePaddingY), Gap = 0f }))
        {
            var text = ui.Memo("closed", (assembly, t: ui.State.Strings), static a => a.t.DocumentClosed(a.assembly));
            ui.Text(text, color: ui.Theme.Warning, wrap: true);
        }
    }

    /// <summary>Records the tooltip and returns the link clicked in it this frame, if one was.</summary>
    public static RichTextClick Render(Ui<UiState> ui, CodeTooltip tooltip)
    {
        var shown = ShownOf(ui, tooltip);
        shown.Clicked = default;
        using (ui.Column(new Box { Gap = 0f }))
        {
            RenderSignature(ui, shown);
            if (shown.Layout.Groups.Count == 0)
            {
                return shown.Clicked;
            }

            ui.Separator();
            RenderDocumentation(ui, shown);
        }
        return shown.Clicked;
    }

    private static void Text(Ui<UiState> ui, ShownTooltip shown, IReadOnlyList<RichTextRun> runs)
    {
        var id = ui.GetId(RuntimeHelpers.GetHashCode(runs));
        var click = ui.RichText(runs, codeBackground: shown.CodeBackground, id: id);
        if (click.Link is not null)
        {
            shown.Clicked = click;
        }
    }

    private static void RenderSignature(Ui<UiState> ui, ShownTooltip shown)
    {
        using (ui.Column(new Box { Padding = new Spacing(PaddingX, SignaturePaddingY), Gap = ContextGap }))
        {
            RenderDeclaration(ui, shown);
            if (shown.Layout.Container.Count > 0)
            {
                Text(ui, shown, shown.Layout.Container);
            }

            RenderConstraints(ui, shown);
        }
    }

    private static void RenderConstraints(Ui<UiState> ui, ShownTooltip shown)
    {
        if (shown.Layout.Constraints.Count == 0)
        {
            return;
        }

        using (ui.Column(new Box { Padding = new Spacing(0f, ConstraintGap, 0f, 0f), Gap = 0f }))
        {
            foreach (var constraint in shown.Layout.Constraints)
            {
                Text(ui, shown, constraint);
            }
        }
    }

    private static void RenderDeclaration(Ui<UiState> ui, ShownTooltip shown)
    {
        if (FitsOnOneLine(ui, shown))
        {
            Text(ui, shown, shown.Layout.Declaration);
            return;
        }
        using (ui.Column(new Box { Gap = 0f }))
        {
            Text(ui, shown, shown.Layout.DeclarationHead);
            using (ui.Column(new Box { Padding = new Spacing(ParameterIndent, 0f, 0f, 0f), Gap = 0f }))
            {
                foreach (var parameter in shown.Layout.Parameters)
                {
                    Text(ui, shown, parameter);
                }
            }
        }
    }

    private static bool FitsOnOneLine(Ui<UiState> ui, ShownTooltip shown)
    {
        if (shown.Layout.Parameters.Count == 0)
        {
            return true;
        }

        var room = MaxWidth - 2 * PaddingX;
        return ui.MeasureRuns(shown.Layout.Declaration) <= room;
    }

    private static void RenderDocumentation(Ui<UiState> ui, ShownTooltip shown)
    {
        using (ui.Column(new Box { Padding = new Spacing(PaddingX, DocumentationPaddingY), Gap = SectionGap }))
        {
            foreach (var group in shown.Layout.Groups)
            {
                RenderGroup(ui, shown, group);
            }
        }
    }

    private static void RenderGroup(Ui<UiState> ui, ShownTooltip shown, TooltipGroup group)
    {
        using (ui.Column(new Box { Gap = HeadingGap }))
        {
            if (group.Heading is not null)
            {
                Text(ui, shown, group.Heading);
            }

            var labelWidth = LabelWidth(ui, group.Entries);
            using (ui.Column(new Box { Gap = EntryGap }))
            {
                foreach (var entry in group.Entries)
                {
                    RenderEntry(ui, shown, group.Shape, entry, labelWidth);
                }
            }
        }
    }

    private static float LabelWidth(Ui<UiState> ui, List<TooltipEntry> entries)
    {
        var widest = 0f;
        foreach (var entry in entries)
        {
            if (entry.Label is not null)
            {
                widest = MathF.Max(widest, ui.MeasureRuns(entry.Label));
            }
        }
        return MathF.Min(widest, MaxLabelWidth) + ColumnGap;
    }

    private static void RenderEntry(Ui<UiState> ui, ShownTooltip shown, EntryShape shape, TooltipEntry entry, float labelWidth)
    {
        switch (shape)
        {
            case EntryShape.Column when entry.Label is not null:
                RenderColumnEntry(ui, shown, entry.Label, entry.Blocks, labelWidth);
                break;
            case EntryShape.Stacked when entry.Label is not null:
                RenderStackedEntry(ui, shown, entry.Label, entry.Blocks);
                break;
            default:
                RenderBlocks(ui, shown, entry.Blocks);
                break;
        }
    }

    private static void RenderColumnEntry(Ui<UiState> ui, ShownTooltip shown, IReadOnlyList<RichTextRun> label, IReadOnlyList<TooltipBlock> blocks,
        float labelWidth)
    {
        using (ui.Row(new Box { Gap = 0f }))
        {
            using (ui.Column(new Box { Width = labelWidth }))
            {
                Text(ui, shown, label);
            }

            using (ui.Column(new Box { Width = Sizing.Grow() }))
            {
                RenderBlocks(ui, shown, blocks);
            }
        }
    }

    private static void RenderStackedEntry(Ui<UiState> ui, ShownTooltip shown, IReadOnlyList<RichTextRun> label, IReadOnlyList<TooltipBlock> blocks)
    {
        using (ui.Column(new Box { Gap = ExceptionGap }))
        {
            Text(ui, shown, label);
            using (ui.Column(new Box { Padding = new Spacing(ExceptionIndent, 0f, 0f, 0f) }))
            {
                RenderBlocks(ui, shown, blocks);
            }
        }
    }

    private static void RenderBlocks(Ui<UiState> ui, ShownTooltip shown, IReadOnlyList<TooltipBlock> blocks)
    {
        using (ui.Column(new Box { Width = Sizing.Grow(), Gap = ParagraphGap }))
        {
            for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                var block = blocks[blockIndex];
                RenderBlock(ui, shown, block);
            }
        }
    }

    private static void RenderBlock(Ui<UiState> ui, ShownTooltip shown, TooltipBlock block)
    {
        switch (block)
        {
            case TextBlock text:
                Text(ui, shown, text.Runs);
                break;
            case CodeBlock code:
                RenderCode(ui, shown, code);
                break;
            case ListBlock list:
                RenderList(ui, shown, list);
                break;
        }
    }

    private static void RenderCode(Ui<UiState> ui, ShownTooltip shown, CodeBlock code)
    {
        var box = new Box
        {
            Width = Sizing.Grow(),
            Padding = new Spacing(CodeBlockPadding),
            Radius = CodeBlockRadius,
            Background = shown.CodeBlockBackground,
        };
        using (ui.Column(box))
        {
            Text(ui, shown, code.Runs);
        }
    }

    // Each row's lead (a marker, or a table's term) gets a column of its own so that wrapped lines hang under the text
    // rather than the lead.
    private static void RenderList(Ui<UiState> ui, ShownTooltip shown, ListBlock list)
    {
        var leadWidth = LeadWidth(ui, list);
        using (ui.Column(new Box { Width = Sizing.Grow(), Gap = ItemGap }))
        {
            var rows = list.Rows;
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                RenderListRow(ui, shown, row, leadWidth);
            }
        }
    }

    private static float LeadWidth(Ui<UiState> ui, ListBlock list)
    {
        var widest = 0f;
        var rows = list.Rows;
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            widest = MathF.Max(widest, ui.MeasureRuns(row.Lead));
        }

        if (list.IsTable)
        {
            return MathF.Min(widest, MaxLabelWidth) + ColumnGap;
        }

        return MathF.Max(MarkerWidth, widest + MarkerGap);
    }

    private static void RenderListRow(Ui<UiState> ui, ShownTooltip shown, ListRow row, float leadWidth)
    {
        using (ui.Row(new Box { Gap = 0f }))
        {
            using (ui.Column(new Box { Width = leadWidth }))
            {
                Text(ui, shown, row.Lead);
            }

            using (ui.Column(new Box { Width = Sizing.Grow() }))
            {
                Text(ui, shown, row.Content);
            }
        }
    }

    private sealed class ShownTooltip
    {
        public CodeTooltip? Tooltip;
        public CodeViewPalette? Palette;
        public UiTheme? Theme;
        public Strings? Strings;
        public uint CodeBackground;
        public uint CodeBlockBackground;
        public RichTextClick Clicked;
        public TooltipLayout Layout = TooltipLayout.Empty;
    }

    private static bool IsBuiltFor(ShownTooltip shown, CodeTooltip tooltip, CodeViewPalette palette, UiTheme theme, Strings strings)
    {
        var sameLook = ReferenceEquals(shown.Palette, palette) && ReferenceEquals(shown.Theme, theme);
        return ReferenceEquals(shown.Tooltip, tooltip) && sameLook && ReferenceEquals(shown.Strings, strings);
    }

    private static ShownTooltip ShownOf(Ui<UiState> ui, CodeTooltip tooltip)
    {
        var t = ui.State.Strings;
        var shown = ui.GetState<ShownTooltip>(ui.GetId("code-tooltip"));
        var palette = ui.State.CodeViewPalette;
        if (IsBuiltFor(shown, tooltip, palette, ui.Theme, t))
        {
            return shown;
        }

        shown.Tooltip = tooltip;
        shown.Palette = palette;
        shown.Theme = ui.Theme;
        shown.Strings = t;
        shown.CodeBackground = UiColor.Mix(ui.Theme.SurfaceRaised, ui.Theme.Text, CodeChipMix);
        shown.CodeBlockBackground = UiColor.Mix(ui.Theme.SurfaceRaised, ui.Theme.EditorBackground, CodeBlockMix);
        shown.Layout = TooltipLayouts.Build(CreateStyles(ui, palette), t, tooltip);
        return shown;
    }

    private static TooltipStyles CreateStyles(Ui<UiState> ui, CodeViewPalette palette)
    {
        var theme = ui.Theme;
        var size = theme.FontSize;
        return new TooltipStyles
        {
            Plain = ui.StyleText(theme.Text, size),
            Bold = ui.StyleText(theme.Text, size, bold: true),
            Italic = ui.StyleText(theme.Text, size, family: TextFonts.Italic),
            Heading = ui.StyleText(GetHeadingColor(theme), theme.FontSizeSmall, bold: true),
            Container = ui.StyleText(theme.TextMuted, size, family: MonoFonts.Code),
            CodeBlock = ui.StyleText(theme.Text, size, family: MonoFonts.Code),
            Code = kind => ui.StyleText(palette[kind], size, family: MonoFonts.Code),
            Reference = kind => ui.StyleText(palette[kind], size),
        };
    }

    private static uint GetHeadingColor(UiTheme theme)
    {
        return UiColor.Mix(theme.TextMuted, theme.Text, HeadingMix);
    }
}
