using ZGF.Gui.Immediate;
using ILSight.Core.Metadata;

namespace ILSight.MetadataArea;

/// <summary>What hovering a metadata grid's cell explains: text, or a flags value broken down into its
/// groups.</summary>
internal static class MetadataTooltipView
{
    public static void Render(Ui ui, Appearance appearance, MetadataTooltip content)
    {
        var theme = ui.Theme;
        if (content.Text is not null)
        {
            ui.Text(content.Text, color: theme.Text, size: theme.FontSizeSmall, wrap: true);
            return;
        }
        using (ui.Column(new Box { Gap = theme.GapTight }))
        {
            var groups = content.Groups;
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                Group(ui, appearance, group);
            }
        }
    }

    private static void Group(Ui ui, Appearance appearance, MetadataFlagGroup group)
    {
        if (group.IsChoice)
        {
            Choice(ui, group);
        }
        else
        {
            Flags(ui, appearance, group);
        }
    }

    private static void Choice(Ui ui, MetadataFlagGroup group)
    {
        var theme = ui.Theme;
        using (ui.Row(new Box { Gap = theme.GapTight }))
        {
            Header(ui, group);
            if (group.Flags.Count > 0)
            {
                ui.Text(group.Flags[0].Name, color: theme.Text, size: theme.FontSizeSmall);
            }
        }
    }

    private static void Flags(Ui ui, Appearance appearance, MetadataFlagGroup group)
    {
        using (ui.Column(new Box { Gap = 0f }))
        {
            Header(ui, group);
            var flags = group.Flags;
            for (var flagIndex = 0; flagIndex < flags.Count; flagIndex++)
            {
                var flag = flags[flagIndex];
                Flag(ui, appearance, flag);
            }
        }
    }

    private static void Header(Ui ui, MetadataFlagGroup group)
    {
        if (string.IsNullOrEmpty(group.Header))
        {
            return;
        }

        var theme = ui.Theme;
        ui.Text(group.Header, color: theme.Text, size: theme.FontSizeSmall, bold: true);
    }

    private static void Flag(Ui ui, Appearance appearance, MetadataFlag flag)
    {
        var theme = ui.Theme;
        var icons = appearance.Icons;
        var box = flag.IsSet ? icons.MetadataFlagSet : icons.MetadataFlagUnset;
        var boxColor = flag.IsSet ? theme.AccentText : theme.TextMuted;
        var nameColor = flag.IsSet ? theme.Text : theme.TextMuted;
        using (ui.Row(new Box { Gap = theme.GapTight, AlignY = Align.Center }))
        {
            ui.Glyph(box, boxColor, theme.FontSizeSmall);
            ui.Text(flag.Name, color: nameColor, size: theme.FontSizeSmall);
        }
    }
}
