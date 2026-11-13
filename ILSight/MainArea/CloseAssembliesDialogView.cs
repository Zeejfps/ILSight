using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CommandArea;
using ILSight.AssemblyArea;

namespace ILSight.MainArea;

/// <summary>Draws the "close assemblies" dialog: every item of the list to tick, filtered by name or path as
/// the export dialog's assemblies are, and Close, which closes the ticked ones.</summary>
internal static class CloseAssembliesDialogView
{
    private const string FilterId = "close-filter";

    public static void Render(Ui<UiState> ui, AssemblyList assemblies)
    {
        var closer = ui.State.Closer;
        var wasOpen = closer.IsOpen;
        if (wasOpen)
        {
            closer.Rows.Update(assemblies);
        }

        var t = ui.State.Strings;
        var close = false;
        var cancel = false;
        using (var dialog = ui.WindowDialog("close-assemblies", t.CloseAssembliesTitle, ref closer.IsOpen,
            AppLayout.CloseDialogWidth, AppLayout.CloseDialogHeight, ref closer.Window.Bounds,
            minWidth: AppLayout.CloseDialogMinWidth, minHeight: AppLayout.CloseDialogMinHeight))
        {
            if (dialog.IsOpen)
            {
                Content(ui, closer, ref close, ref cancel);
            }
        }

        if (close)
        {
            var included = closer.Rows.CountIncluded();
            if (included > 0)
            {
                Commands.Later(ui.State, closer.Rows.ListToClose(), static (a, items) => Commands.CloseAssemblies(a, items));
                cancel = true;
            }
        }
        // Escape or the close button only clear IsOpen.
        var dismissed = wasOpen && !closer.IsOpen;
        if (cancel || dismissed)
        {
            closer.Close();
        }

        closer.Window.Settle(ui.Input.IsDown(PointerButton.Left));
    }

    private static void Content(Ui<UiState> ui, AssemblyCloser closer, ref bool close, ref bool cancel)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var shown = AssemblyClosing.ListShown(closer);
        using (ui.Row(new Box { Gap = theme.Gap, AlignY = Align.Center }))
        {
            if (closer.Rows.TryTakeFilterFocus())
            {
                ui.Focus(ui.GetId(FilterId));
            }

            close |= ui.TextField(FilterId, ref closer.Filter, t.ExportFilter, Sizing.Grow(), ui.State.Icons.ExportFilter, clearable: true);
            ui.Checkbox(t.ExportSelectedOnly, ref closer.SelectedOnly, id: "close-selected-only");
        }
        using (ui.Row(new Box { Padding = new Spacing(theme.Gap + theme.BorderWidth, 0f, 0f, 0f) }))
        {
            var all = AssemblyClosing.CalculateAllShownIncluded(shown);
            if (ui.Checkbox(t.ExportSelectShown, ref all, id: "close-all", compact: true))
            {
                AssemblyClosing.IncludeShown(shown, all == true);
            }
        }
        using (ui.Column(ListFrame(theme)))
        {
            if (shown.Count == 0)
            {
                NoMatch(ui, closer);
            }
            else
            {
                Rows(ui, closer, shown);
            }
        }

        using (ui.Row(new Box { Gap = theme.GapTight }))
        {
            var count = closer.Rows.CountIncluded();
            var summary = ui.Memo("close-count", (count, t), static a => a.t.PickerSelected(a.count));
            ui.Text(summary, color: theme.TextMuted, size: theme.FontSizeSmall);
            ui.Spacer();
            cancel |= ui.Button(t.PickerCancel, id: "close-cancel");
            close |= ui.Button(t.FileClose, enabled: count > 0, id: "close-ticked", variant: ButtonVariant.Primary);
        }
    }

    /// <summary>A bordered list on the field color that fills the dialog, its rows clipped to its corners.</summary>
    private static Box ListFrame(UiTheme theme)
    {
        return new Box()
        {
            Height = Sizing.Grow(),
            Gap = 0f,
            Background = theme.Field,
            BorderColor = theme.Border,
            BorderWidth = theme.BorderWidth,
            Radius = theme.RadiusSmall,
            Clip = true,
        };
    }

    /// <summary>Says that the filter hides every item, with a way to clear it.</summary>
    private static void NoMatch(Ui<UiState> ui, AssemblyCloser closer)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        using (ui.Column(new Box { Height = Sizing.Grow(), Gap = theme.Gap, AlignX = Align.Center, AlignY = Align.Center }))
        {
            ui.Text(t.ExportNoMatch, color: theme.TextMuted);
            if (ui.Button(t.ExportClearFilter, id: "close-clear-filter", variant: ButtonVariant.Ghost))
            {
                closer.ClearFilter();
            }
        }
    }

    private static void Rows(Ui<UiState> ui, AssemblyCloser closer, List<CloseChoice> shown)
    {
        var theme = ui.Theme;
        using (ui.Scroll("close-choices", new Box { Height = Sizing.Grow(), Padding = new Spacing(0f, theme.GapTight) }))
        {
            foreach (var choice in shown)
            {
                Row(ui, closer, shown, choice);
            }
        }
    }

    /// <summary>An item to tick, with its version, and its file's path on hover. Shift-click ticks the range
    /// from the last row clicked.</summary>
    private static void Row(Ui<UiState> ui, AssemblyCloser closer, List<CloseChoice> shown, CloseChoice choice)
    {
        var theme = ui.Theme;
        var assembly = choice.Assembly;
        using (ui.Id(assembly.FilePath.Text))
        using (ui.Row(new Box { Gap = theme.Gap, Padding = new Spacing(theme.Gap, 0f) }))
        {
            var width = Sizing.Grow();
            if (ui.Checkbox(assembly.Name, ref choice.Included, id: "include", width: width, compact: true))
            {
                closer.Rows.TickClicked(shown, choice, (ui.Input.Modifiers & KeyModifiers.Shift) != 0);
            }

            ui.Tooltip(ui.GetId("include"), assembly.FilePath.Text, TooltipAnchor.Pointer);
            if (assembly.Version is not null)
            {
                ui.Text(assembly.Version.ToString(), color: theme.TextMuted, size: theme.FontSizeSmall);
            }
        }
    }
}
