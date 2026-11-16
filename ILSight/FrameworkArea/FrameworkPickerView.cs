using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.CommandArea;
using ILSight.UserArea;

namespace ILSight.FrameworkArea;

internal static class FrameworkPickerView
{
    /// <summary>Opens the picker, drawn from this frame on, and asks for the runtime to be scanned for it once the
    /// frame is drawn, unless it was: the picker draws it scanning from this frame on.</summary>
    public static void Open(IAppCommandWriter commands, FrameworkPicker picker)
    {
        var scan = picker.Open();
        if (scan is null)
        {
            return;
        }

        Commands.StartSettling(commands, nameof(FrameworkBrowser.ScanAsync), (Browser: picker.Browser, Scan: scan), static (_, s, ct) => s.Browser.ScanAsync(s.Scan, ct),
            static (_, s, error) => s.Browser.FailScan(s.Scan, error));
    }

    public static void Render(Ui ui, Appearance appearance, IAppCommandWriter commands, FrameworkPicker frameworkPicker, Navigation navigation, AssemblyList assemblies)
    {
        var t = appearance.Strings;
        var picker = frameworkPicker;
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
                open |= browser.Scan.Match((ui, appearance, browser, listId),
                    notStarted: static (s, _) => NoteScanning(s.ui, s.appearance),
                    scanning: static (s, _) => NoteScanning(s.ui, s.appearance),
                    scanned: static (s, _) => Rows(s.ui, s.appearance, s.browser, s.listId),
                    failed: static (s, failed) => NoteScanFailed(s.ui, s.appearance, failed.Error));
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
            Commands.OpenFilesLater(commands, navigation.BeginOpening(ListPaths(browser.ListSelected())));
            ui.RequestUpdate();
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

    private static bool NoteScanning(Ui ui, Appearance appearance)
    {
        var theme = ui.Theme;
        ui.Text(appearance.Strings.PickerScanning, new Box { Padding = new Spacing(theme.ShellPaddingX, theme.Gap) }, color: theme.TextMuted);
        return false;
    }

    private static bool NoteScanFailed(Ui ui, Appearance appearance, string error)
    {
        var theme = ui.Theme;
        ui.Text(appearance.Strings.PickerScanFailed, new Box { Padding = new Spacing(theme.ShellPaddingX, theme.Gap) },
            color: theme.TextMuted, wrap: true);
        ui.Text(error, new Box { Padding = new Spacing(theme.ShellPaddingX, theme.Gap) }, color: theme.TextMuted, wrap: true);
        return false;
    }

    private static void MoveSelectionFromFilter(Ui ui, FrameworkBrowser browser, int listId)
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

    private static bool Rows(Ui ui, Appearance appearance, FrameworkBrowser browser, int listId)
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
                NodeIcons.Draw(ui, appearance, NodeIcon.Assembly);
                using (ui.Row(new Box { Gap = theme.Gap, AlignY = Align.Center }))
                {
                    ui.Text(OneLineText.Name(assembly.Name), new Box { Width = Sizing.Grow() }, color: nameColor, overflow: TextOverflow.Ellipsis);
                    ui.Text(OneLineText.Name(assembly.Framework), color: theme.TextMuted, size: theme.FontSizeSmall);
                }
            }
        }
        return list.Activated;
    }
}
