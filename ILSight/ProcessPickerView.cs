using System.Diagnostics;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ZGF.KeyboardModule;
using ILSight.Core.Processes;
using ILSight.Localization;
using static ILSight.Assemblies;
using static ILSight.ProcessPicking;

namespace ILSight;

/// <summary>Draws the "open from running process" dialog: the processes on the left, the assemblies the
/// selected one has loaded on the right.</summary>
internal static class ProcessPickerView
{
    private const string FilterId = "process-filter";
    private const string ProcessListId = "process-list";
    private const string ModuleListId = "process-modules";

    public static void Render(Ui<UiState> ui)
    {
        var picker = ui.State.ProcessPicker;
        var wasOpen = picker.IsOpen;
        if (wasOpen)
            UpdatePicker(picker);

        var open = false;
        var close = false;
        var card = new Box { Width = Sizing.Grow(0f, AppLayout.ProcessPickerWidth), Height = Sizing.Percent(AppLayout.PickerHeight) };
        using (var dialog = ui.Dialog("process-picker", ui.State.Strings.ProcessPickerTitle, ref picker.IsOpen, card))
        {
            if (dialog.IsOpen)
                Content(ui, picker, ref open, ref close);
        }

        if (open && CanOpen(picker))
        {
            Commands.Later(ui.State, ListToOpen(picker), static (a, paths) => OpenAssemblies(a, paths));
            close = true;
        }
        // Escape, the close button or the backdrop only clear IsOpen.
        var dismissed = wasOpen && !picker.IsOpen;
        if (close || dismissed)
            ClosePicker(picker);
    }

    private static void Content(Ui<UiState> ui, ProcessPicker picker, ref bool open, ref bool close)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var processListId = ui.GetId(ProcessListId);
        if (ui.HasFocus(ui.GetId(FilterId)))
            MoveSelectionFromFilter(ui, picker, processListId);
        if (TakeFilterFocus(picker))
            ui.Focus(ui.GetId(FilterId));
        open |= ui.TextField(FilterId, ref picker.Filter, t.ProcessPickerFilter, clearable: true);

        using (ui.Row(new Box { Height = Sizing.Grow(), Gap = theme.Gap, AlignY = Align.Start }))
        {
            using (Pane(ui))
                open |= Processes(ui, picker, processListId);
            using (Pane(ui))
                open |= Modules(ui, picker);
        }

        using (ui.Row(new Box { Gap = theme.GapTight }))
        {
            var counts = (Processes: picker.Filtered.Count, Modules: GetModules(picker.Modules).Count, Selected: picker.ModuleSelection.Count, t);
            var summary = ui.Memo("process-count", counts, static a => Summary(a.t, a.Processes, a.Modules, a.Selected));
            ui.Text(summary, color: theme.TextMuted, size: theme.FontSizeSmall);
            ui.Spacer();
            if (ui.Button(t.ProcessPickerRefresh, enabled: picker.Scan is not ProcessScan.Scanning, id: "process-refresh"))
                Refresh(picker);
            close |= ui.Button(t.PickerCancel, id: "process-cancel");
            open |= ui.Button(t.PickerOpen, enabled: CanOpen(picker), id: "process-open", variant: ButtonVariant.Primary);
        }
    }

    private static string Summary(Strings t, int processes, int modules, int selected) =>
        $"{t.ProcessPickerCount(processes)} · {t.PickerCount(modules)} · {t.PickerSelected(selected)}";

    private static Ui.Scope Pane(Ui<UiState> ui)
    {
        var theme = ui.Theme;
        return ui.Panel(new Box { Width = Sizing.Grow(), Height = Sizing.Grow(), Padding = theme.BorderWidth, Background = theme.Field, Clip = true });
    }

    /// <summary>Walks the processes from the filter, leaving Home and End to its text.</summary>
    private static void MoveSelectionFromFilter(Ui<UiState> ui, ProcessPicker picker, int listId)
    {
        var page = ui.MeasureListPage(listId, ui.Theme.RowHeight);
        foreach (var stroke in ui.Input.Keys)
        {
            if (stroke.Key is KeyboardKey.Home or KeyboardKey.End)
                continue;
            var index = picker.ProcessSelection.Index;
            if (UiWidgets.ApplyListKey(stroke, ref index, picker.Filtered.Count, page))
                picker.ProcessSelection.Select(index, picker.Filtered.Count);
        }
    }

    private static void Note(Ui<UiState> ui, string text)
    {
        var theme = ui.Theme;
        ui.Text(text, new Box { Padding = new Spacing(theme.ShellPaddingX, theme.Gap) }, color: theme.TextMuted, wrap: true);
    }

    /// <summary>The processes, or why there are none. True when one was activated.</summary>
    private static bool Processes(Ui<UiState> ui, ProcessPicker picker, int listId)
    {
        var t = ui.State.Strings;
        switch (picker.Scan)
        {
            case ProcessScan.NotStarted:
            case ProcessScan.Scanning:
                Note(ui, t.ProcessPickerScanning);
                return false;
            case ProcessScan.Failed:
                Note(ui, t.ProcessPickerScanFailed);
                return false;
            case ProcessScan.Scanned scanned:
                if (scanned.Processes.Count == 0)
                {
                    Note(ui, t.ProcessPickerNone);
                    return false;
                }
                return ProcessRows(ui, picker, listId);
            default:
                throw new UnreachableException();
        }
    }

    private static bool ProcessRows(Ui<UiState> ui, ProcessPicker picker, int listId)
    {
        var theme = ui.Theme;
        var t = ui.State.Strings;
        using var list = ui.SelectableList(listId, picker.Filtered.Count, picker.ProcessSelection);
        for (var i = list.First; i < list.End; i++)
        {
            var process = picker.Filtered[i];
            using (ui.Id(process.Id.Value))
            using (var row = list.Row(i, ui.GetId("row"), new Box { Gap = theme.Gap, AlignY = Align.Center }))
            {
                var nameColor = row.Selected ? theme.SelectionText : theme.Text;
                ui.Text(process.Name, new Box { Width = Sizing.Grow() }, color: nameColor, overflow: TextOverflow.Ellipsis);
                ui.Text(process.Id.ToString(), color: theme.TextMuted, size: theme.FontSizeSmall);
                var runtime = process.Info == null ? t.ProcessPickerNoAnswer : process.Info.RuntimeVersion ?? "";
                ui.Text(runtime, color: theme.TextMuted, size: theme.FontSizeSmall);
            }
        }
        return list.Activated;
    }

    /// <summary>The selected process's assemblies, or why they aren't shown. True when some were activated.</summary>
    private static bool Modules(Ui<UiState> ui, ProcessPicker picker)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var entry = GetEntryAssembly(picker.Modules);
        if (entry != null)
        {
            ui.Text(t.ProcessPickerEntry(entry.FileName), new Box { Padding = new Spacing(theme.ShellPaddingX, theme.GapTight) },
                color: theme.TextMuted, size: theme.FontSizeSmall, overflow: TextOverflow.Ellipsis);
        }
        switch (picker.Modules)
        {
            case ModuleScan.None:
                Note(ui, t.ProcessPickerPickProcess);
                return false;
            case ModuleScan.Loading:
                Note(ui, t.ProcessPickerLoading);
                return false;
            case ModuleScan.Unreachable unreachable:
                Note(ui, t.ProcessPickerUnreachable);
                Note(ui, unreachable.Detail);
                return false;
            case ModuleScan.Unreadable unreadable:
                Note(ui, t.ProcessPickerUnreadable);
                Note(ui, unreadable.Detail);
                return false;
            case ModuleScan.Loaded loaded:
                return ModuleRows(ui, picker, loaded.Modules);
            default:
                throw new UnreachableException();
        }
    }

    private static bool ModuleRows(Ui<UiState> ui, ProcessPicker picker, IReadOnlyList<RunningModule> modules)
    {
        var theme = ui.Theme;
        var t = ui.State.Strings;
        using var list = ui.SelectableList(ui.GetId(ModuleListId), modules, picker.ModuleSelection);
        for (var i = list.First; i < list.End; i++)
        {
            var module = modules[i];
            using (ui.Id(i))
            using (var row = list.Row(i, ui.GetId("row"), new Box { Gap = theme.GapTight }))
            {
                var nameColor = row.Selected ? theme.SelectionText : theme.Text;
                NodeIcons.Draw(ui, NodeIcon.Assembly);
                using (ui.Row(new Box { Gap = theme.Gap, AlignY = Align.Center }))
                {
                    switch (module)
                    {
                        case RunningModule.OnDisk onDisk:
                            ui.Text(onDisk.Name, color: nameColor, overflow: TextOverflow.Ellipsis);
                            ui.Text(Path.GetDirectoryName(onDisk.Path.Text) ?? "", new Box { Width = Sizing.Grow() },
                                color: theme.TextMuted, size: theme.FontSizeSmall, overflow: TextOverflow.Ellipsis);
                            break;
                        case RunningModule.InMemory inMemory:
                            ui.Text(inMemory.Name, new Box { Width = Sizing.Grow() }, color: theme.TextMuted, overflow: TextOverflow.Ellipsis);
                            ui.Text(t.ProcessPickerInMemory, color: theme.TextMuted, size: theme.FontSizeSmall);
                            break;
                        default:
                            throw new UnreachableException();
                    }
                }
            }
        }
        return list.Activated;
    }
}
