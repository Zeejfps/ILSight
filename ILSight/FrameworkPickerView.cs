using System.Diagnostics;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;
using ZGF.KeyboardModule;
using ILSight.Commanding;

namespace ILSight;

internal static class FrameworkPickerView
{
    /// <summary>Opens the picker, drawn from this frame on, and asks for the runtime to be scanned for it once the
    /// frame is drawn, unless it was: the picker draws it scanning from this frame on.</summary>
    public static void Open(UiState uiState)
    {
        var picker = uiState.Picker;
        var scan = picker.Open();
        if (scan is null)
        {
            return;
        }

        Commands.Start(uiState, nameof(FrameworkBrowser.ScanAsync), (Browser: picker.Browser, Scan: scan), static (_, s, ct) => s.Browser.ScanAsync(s.Scan, ct));
    }

    public static void Render(Ui<UiState> ui, UserIntent intent, AssemblyList assemblies)
    {
        var t = ui.State.Strings;
        var picker = ui.State.Picker;
        var browser = picker.Browser;
        picker.Update(assemblies);

        var theme = ui.Theme;
        var open = false;
        var close = false;
        var card = new Box { Width = Sizing.Grow(0f, AppLayout.PickerWidth), Height = Sizing.Percent(AppLayout.PickerHeight) };
        using (var dialog = ui.Dialog("picker", t.PickerTitle, ref picker.IsOpen, card))
        {
            if (!dialog.IsOpen)
            {
                return;
            }

            var listId = ui.GetId("picker-list");
            if (!ui.HasFocus(listId))
            {
                MoveSelectionFromFilter(ui, browser, listId);
            }

            if (browser.TryTakeFilterFocus())
            {
                ui.Focus(ui.GetId("picker-filter"));
            }

            open |= ui.TextField("picker-filter", ref picker.Filter, t.PickerFilter, clearable: true);

            using (ui.Panel(new Box { Height = Sizing.Grow(), Padding = theme.BorderWidth, Background = theme.Field, Clip = true }))
            {
                switch (browser.Scan)
                {
                    case RuntimeScan.NotStarted:
                    case RuntimeScan.Scanning:
                        ui.Text(t.PickerScanning, new Box { Padding = new Spacing(theme.ShellPaddingX, theme.Gap) }, color: theme.TextMuted);
                        break;
                    case RuntimeScan.Failed:
                        ui.Text(t.PickerScanFailed, new Box { Padding = new Spacing(theme.ShellPaddingX, theme.Gap) },
                            color: theme.TextMuted, wrap: true);
                        break;
                    case RuntimeScan.Scanned:
                        open |= Rows(ui, browser, listId);
                        break;
                    default:
                        throw new UnreachableException();
                }
            }

            using (ui.Row(new Box { Gap = theme.GapTight }))
            {
                var counts = (Shown: browser.Filtered.Count, Selected: browser.Selection.Count, t);
                var summary = ui.Memo("picker-count", counts, static a => Summary(a.t, a.Shown, a.Selected));
                ui.Text(summary, color: theme.TextMuted, size: theme.FontSizeSmall);
                ui.Spacer();
                close |= ui.Button(t.PickerCancel, id: "picker-cancel");
                open |= ui.Button(t.PickerOpen, enabled: browser.HasSelection, id: "picker-open", variant: ButtonVariant.Primary);
            }
        }

        if (open && browser.HasSelection)
        {
            Commands.OpenFilesLater(ui.State, intent.BeginOpening(ListPaths(browser.ListSelected())));
            close = true;
        }
        if (close)
        {
            picker.IsOpen = false;
        }
    }

    private static string Summary(Strings t, int shown, int selected)
    {
        return $"{t.PickerCount(shown)} · {t.PickerSelected(selected)}";
    }

    private static List<AssemblyPath> ListPaths(List<RuntimeAssembly> selected)
    {
        return selected
            .Select(static a => a.FilePath)
            .ToList();
    }

    /// <summary>Walks the list from the filter, leaving Home and End to its text.</summary>
    private static void MoveSelectionFromFilter(Ui<UiState> ui, FrameworkBrowser browser, int listId)
    {
        var page = ui.MeasureListPage(listId, ui.Theme.RowHeight);
        foreach (var stroke in ui.Input.Keys)
        {
            var movesCaret = stroke.Key is KeyboardKey.Home or KeyboardKey.End;
            if (!movesCaret)
            {
                UiWidgets.ApplyListKey(stroke, browser.Selection, browser.Filtered, page);
            }
        }
    }

    private static bool Rows(Ui<UiState> ui, FrameworkBrowser browser, int listId)
    {
        var theme = ui.Theme;
        using var list = ui.SelectableList(listId, browser.Filtered, browser.Selection);
        for (var i = list.First; i < list.End; i++)
        {
            var assembly = browser.Filtered[i];
            using (ui.Id(assembly.FilePath.Text))
            using (var row = list.Row(i, ui.GetId("row"), new Box { Gap = theme.GapTight }))
            {
                var nameColor = row.Selected ? theme.SelectionText : theme.Text;
                NodeIcons.Draw(ui, NodeIcon.Assembly);
                using (ui.Row(new Box { Gap = theme.Gap, AlignY = Align.Center }))
                {
                    ui.Text(assembly.Name, new Box { Width = Sizing.Grow() }, color: nameColor, overflow: TextOverflow.Ellipsis);
                    ui.Text(assembly.Framework, color: theme.TextMuted, size: theme.FontSizeSmall);
                }
            }
        }
        return list.Activated;
    }
}
