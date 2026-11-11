using System.Diagnostics;
using System.Runtime.CompilerServices;
using ZGF.Desktop;
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

    private static readonly ConditionalWeakTable<Strings, string[]> ProcessHeaders = new();
    private static readonly ConditionalWeakTable<Strings, string[]> ModuleHeaders = new();

    /// <summary>The share of the dialog's width the processes take; the assemblies, whose folders are
    /// shown too, take the rest.</summary>
    private const float ProcessPaneShare = 0.4f;

    public static void Render(Ui<UiState> ui)
    {
        var picker = ui.State.ProcessPicker;
        var wasOpen = picker.IsOpen;
        if (wasOpen)
        {
            UpdatePicker(picker);
        }

        var open = false;
        var close = false;
        using (var dialog = ui.WindowDialog("process-picker", ui.State.Strings.ProcessPickerTitle, ref picker.IsOpen,
            AppLayout.ProcessDialogWidth, AppLayout.ProcessDialogHeight, ref picker.Window.Bounds,
            minWidth: AppLayout.ProcessDialogMinWidth, minHeight: AppLayout.ProcessDialogMinHeight))
        {
            if (dialog.IsOpen)
            {
                Content(ui, picker, ref open, ref close);
            }
        }

        if (open && CanOpen(picker))
        {
            Commands.Start(ui.State, nameof(OpenAssembliesAsync), ListToOpen(picker), static (a, paths, ct) => OpenAssembliesAsync(a, paths, a.AssemblyPorts, ct));
            close = true;
        }
        // Escape or the close button only clear IsOpen.
        var dismissed = wasOpen && !picker.IsOpen;
        if (close || dismissed)
        {
            ClosePicker(picker);
        }

        DialogWindows.Settle(picker.Window, ui.Input.IsDown(PointerButton.Left));
    }

    private static void Content(Ui<UiState> ui, ProcessPicker picker, ref bool open, ref bool close)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var processListId = ui.GetId(ProcessListId);
        var filterId = ui.GetId(FilterId);
        if (ui.HasFocus(filterId))
        {
            MoveSelectionFromFilter(ui, picker, processListId);
        }

        if (TakeFilterFocus(picker))
        {
            ui.Focus(ui.GetId(FilterId));
        }

        open |= ui.TextField(FilterId, ref picker.Filter, t.ProcessPickerFilter, clearable: true);

        using (ui.Row(new Box { Height = Sizing.Grow(), Gap = theme.Gap, AlignY = Align.Start }))
        {
            using (Pane(ui, ProcessPaneShare))
            {
                open |= Processes(ui, picker, processListId);
            }

            using (Pane(ui, 1f - ProcessPaneShare))
            {
                open |= Modules(ui, picker);
            }
        }

        using (ui.Row(new Box { Gap = theme.GapTight }))
        {
            var counts = (Processes: picker.Filtered.Count, Modules: GetModules(picker.Modules).Count, Selected: picker.ModuleSelection.Count, t);
            var summary = ui.Memo("process-count", counts, static a => Summary(a.t, a.Processes, a.Modules, a.Selected));
            ui.Text(summary, color: theme.TextMuted, size: theme.FontSizeSmall);
            ui.Spacer();
            if (ui.Button(t.ProcessPickerRefresh, enabled: picker.Scan is not ProcessScan.Scanning, id: "process-refresh"))
            {
                Refresh(picker);
            }

            close |= ui.Button(t.PickerCancel, id: "process-cancel");
            open |= ui.Button(t.PickerOpen, enabled: CanOpen(picker), id: "process-open", variant: ButtonVariant.Primary);
        }
    }

    private static string Summary(Strings t, int processes, int modules, int selected)
    {
        return $"{t.ProcessPickerCount(processes)} · {t.PickerCount(modules)} · {t.PickerSelected(selected)}";
    }

    /// <summary>A pane taking <paramref name="share"/> of the row, whatever its rows hold, so that neither
    /// pane changes width as rows scroll into view.</summary>
    private static Ui.Scope Pane(Ui<UiState> ui, float share)
    {
        var theme = ui.Theme;
        // No gap: a table's header, the line under it and its rows meet.
        return ui.Panel(new Box
        {
            Width = Sizing.Percent(share), Height = Sizing.Grow(), Gap = 0f, Padding = theme.BorderWidth, Background = theme.Field, Clip = true,
        });
    }

    /// <summary>Walks the processes from the filter, leaving Home and End to its text.</summary>
    private static void MoveSelectionFromFilter(Ui<UiState> ui, ProcessPicker picker, int listId)
    {
        var page = ui.MeasureListPage(listId, ui.Theme.RowHeight);
        foreach (var stroke in ui.Input.Keys)
        {
            if (stroke.Key is KeyboardKey.Home or KeyboardKey.End)
            {
                continue;
            }

            var index = picker.ProcessSelection.Index;
            if (UiWidgets.ApplyListKey(stroke, ref index, picker.Filtered.Count, page))
            {
                picker.ProcessSelection.Select(index, picker.Filtered.Count);
            }
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

    /// <summary>The processes as a table whose columns can be resized: name, id, entry assembly and runtime.
    /// True when one was activated.</summary>
    private static bool ProcessRows(Ui<UiState> ui, ProcessPicker picker, int listId)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var headers = ProcessHeaders.GetValue(t, static t =>
            [t.ProcessPickerColumnName, t.ProcessPickerColumnId, t.ProcessPickerColumnEntry, t.ProcessPickerColumnRuntime]);
        var table = ui.Table(listId, ui.State.ProcessColumns, picker.ProcessSelection);
        table.Header(headers);
        using var body = table.Body(picker.Filtered.Count);
        for (var i = body.First; i < body.End; i++)
        {
            var process = picker.Filtered[i];
            using var row = body.Row(i, process.Id.Value);
            var nameColor = row.Selected ? theme.SelectionText : theme.Text;
            row.TextCell(0, process.Name, nameColor);
            row.TextCell(1, process.Id.ToString(), theme.TextMuted);
            if (process.Info == null)
            {
                row.TextCell(2, "", theme.TextMuted);
                row.TextCell(3, t.ProcessPickerNoAnswer, theme.TextMuted);
                continue;
            }
            row.TextCell(2, process.Info.EntryAssemblyName ?? "", theme.TextMuted);
            row.TextCell(3, process.Info.RuntimeVersion ?? "", theme.TextMuted);
        }
        return body.Activated;
    }

    /// <summary>The selected process's assemblies, or why they aren't shown. True when some were activated.</summary>
    private static bool Modules(Ui<UiState> ui, ProcessPicker picker)
    {
        var t = ui.State.Strings;
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

    /// <summary>The assemblies as a table whose columns can be resized: name, and the folder or that it is
    /// only in memory. True when the picked-out ones were activated.</summary>
    private static bool ModuleRows(Ui<UiState> ui, ProcessPicker picker, IReadOnlyList<RunningModule> modules)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var headers = ModuleHeaders.GetValue(t, static t => [t.ProcessPickerColumnAssembly, t.ProcessPickerColumnFolder]);
        var table = ui.Table(ModuleListId, ui.State.ProcessModuleColumns, picker.ModuleSelection);
        table.Header(headers);
        using var rows = table.Body(modules);
        for (var i = rows.First; i < rows.End; i++)
        {
            using var row = rows.Row(i);
            switch (modules[i])
            {
                case RunningModule.OnDisk onDisk:
                    using (row.Cell(0))
                    {
                        NodeIcons.Draw(ui, NodeIcon.Assembly);
                        ui.Text(onDisk.Name, new Box { Width = Sizing.Grow() }, color: row.Selected ? theme.SelectionText : theme.Text,
                            overflow: TextOverflow.Ellipsis);
                    }
                    row.TextCell(1, Path.GetDirectoryName(onDisk.Path.Text) ?? "", theme.TextMuted, overflow: TextOverflow.EllipsisMiddle);
                    break;
                case RunningModule.InMemory inMemory:
                    using (row.Cell(0))
                    {
                        NodeIcons.Draw(ui, NodeIcon.Assembly);
                        ui.Text(inMemory.Name, new Box { Width = Sizing.Grow() }, color: theme.TextMuted, overflow: TextOverflow.Ellipsis);
                    }
                    row.TextCell(1, t.ProcessPickerInMemory, theme.TextMuted);
                    break;
                default:
                    throw new UnreachableException();
            }
        }
        return rows.Activated;
    }
}
