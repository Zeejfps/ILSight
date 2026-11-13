using System.Runtime.CompilerServices;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ZGF.KeyboardModule;
using ILSight.Core.Processes;
using ILSight.CommandArea;
using ILSight.UserArea;
using ILSight.ILSightAppArea;
using static ILSight.ProcessArea.ProcessPicking;

namespace ILSight.ProcessArea;

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

    /// <summary>Opens the dialog, drawn scanning from this frame on, and asks for the processes to be looked for
    /// again once the frame is drawn, as they come and go.</summary>
    public static void Open(UiState uiState)
    {
        var picker = uiState.ProcessPicker;
        Scan(uiState, picker.Browser, picker.Open());
    }

    /// <summary>Asks for the processes to be looked for once the frame is drawn, as <paramref name="scan"/>, which
    /// the dialog draws from this frame on; another scan or closing the dialog cancels it.</summary>
    private static void Scan(UiState uiState, ProcessBrowser browser, WorkTicket scan)
    {
        Commands.Start(uiState, nameof(ProcessBrowser.ScanAsync), (Browser: browser, Scan: scan), static (_, s, ct) => s.Browser.ScanAsync(s.Scan, ct), scan.Token);
    }

    /// <summary>Draws the dialog, over the filter applied and the modules of the process selected, which start
    /// listing before it draws (see <see cref="AppFrames.FollowProcessPicker"/>); then tells what it found whether
    /// it is still open, which stops what it started once it is not, however it was closed.</summary>
    public static void Render(Ui<UiState> ui, Navigation navigation)
    {
        var picker = ui.State.ProcessPicker;
        var browser = picker.Browser;
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

        if (open && browser.CanOpen)
        {
            Commands.OpenFilesLater(ui.State, navigation.BeginOpening(browser.ListToOpen()));
            ui.RequestUpdate();
            close = true;
        }
        if (close)
        {
            picker.IsOpen = false;
        }

        browser.StopUnlessOpen(picker.IsOpen);
        DialogWindows.Settle(picker.Window, ui.Input.IsDown(PointerButton.Left));
    }

    private static void Content(Ui<UiState> ui, ProcessPicker picker, ref bool open, ref bool close)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var browser = picker.Browser;
        var processListId = ui.GetId(ProcessListId);
        var filterId = ui.GetId(FilterId);
        if (ui.HasFocus(filterId))
        {
            MoveSelectionFromFilter(ui, browser, processListId);
        }

        if (browser.TryTakeFilterFocus())
        {
            ui.Focus(ui.GetId(FilterId));
        }

        open |= ui.TextField(FilterId, ref picker.Filter, t.ProcessPickerFilter, clearable: true);

        using (ui.Row(new Box { Height = Sizing.Grow(), Gap = theme.Gap, AlignY = Align.Start }))
        {
            using (Pane(ui, ProcessPaneShare))
            {
                open |= Processes(ui, browser, processListId);
            }

            using (Pane(ui, 1f - ProcessPaneShare))
            {
                open |= Modules(ui, browser);
            }
        }

        using (ui.Row(new Box { Gap = theme.GapTight }))
        {
            var counts = (Processes: browser.Filtered.Count, Modules: GetModules(browser.Modules).Count, Selected: browser.ModuleSelection.Count, t);
            var summary = ui.Memo("process-count", counts, static a => Summary(a.t, a.Processes, a.Modules, a.Selected));
            ui.Text(summary, color: theme.TextMuted, size: theme.FontSizeSmall);
            ui.Spacer();
            if (ui.Button(t.ProcessPickerRefresh, enabled: browser.Scan is not ProcessScan.Scanning, id: "process-refresh"))
            {
                Scan(ui.State, browser, browser.StartScan());
            }

            close |= ui.Button(t.PickerCancel, id: "process-cancel");
            open |= ui.Button(t.PickerOpen, enabled: browser.CanOpen, id: "process-open", variant: ButtonVariant.Primary);
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
    private static void MoveSelectionFromFilter(Ui<UiState> ui, ProcessBrowser browser, int listId)
    {
        var page = ui.MeasureListPage(listId, ui.Theme.RowHeight);
        foreach (var stroke in ui.Input.Keys)
        {
            if (stroke.Key is KeyboardKey.Home or KeyboardKey.End)
            {
                continue;
            }

            var index = browser.ProcessSelection.Index;
            if (UiWidgets.ApplyListKey(stroke, ref index, browser.Filtered.Count, page))
            {
                browser.ProcessSelection.Select(index, browser.Filtered.Count);
            }
        }
    }

    private static void Note(Ui<UiState> ui, string text)
    {
        var theme = ui.Theme;
        ui.Text(text, new Box { Padding = new Spacing(theme.ShellPaddingX, theme.Gap) }, color: theme.TextMuted, wrap: true);
    }

    /// <summary>The processes, or why there are none. True when one was activated.</summary>
    private static bool Processes(Ui<UiState> ui, ProcessBrowser browser, int listId)
    {
        return browser.Scan.Match((ui, browser, listId),
            notStarted: static (s, _) => NoteInstead(s.ui, s.ui.State.Strings.ProcessPickerScanning),
            scanning: static (s, _) => NoteInstead(s.ui, s.ui.State.Strings.ProcessPickerScanning),
            scanned: static (s, scanned) => ScannedProcesses(s.ui, s.browser, s.listId, scanned),
            failed: static (s, failed) => NoteInstead(s.ui, s.ui.State.Strings.ProcessPickerScanFailed, failed.Error));
    }

    /// <summary>Notes <paramref name="text"/> where the rows would be. False: none was activated.</summary>
    private static bool NoteInstead(Ui<UiState> ui, string text)
    {
        Note(ui, text);
        return false;
    }

    /// <summary>Notes <paramref name="text"/> and then <paramref name="detail"/> where the rows would be. False: none
    /// was activated.</summary>
    private static bool NoteInstead(Ui<UiState> ui, string text, string detail)
    {
        Note(ui, text);
        Note(ui, detail);
        return false;
    }

    /// <summary>The processes <paramref name="scanned"/> found, or that there are none. True when one was
    /// activated.</summary>
    private static bool ScannedProcesses(Ui<UiState> ui, ProcessBrowser browser, int listId, ProcessScan.Scanned scanned)
    {
        if (scanned.Processes.Count == 0)
        {
            return NoteInstead(ui, ui.State.Strings.ProcessPickerNone);
        }

        return ProcessRows(ui, browser, listId);
    }

    /// <summary>The processes as a table whose columns can be resized: name, id, entry assembly and runtime.
    /// True when one was activated.</summary>
    private static bool ProcessRows(Ui<UiState> ui, ProcessBrowser browser, int listId)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var headers = ProcessHeaders.GetValue(t, static t =>
            [t.ProcessPickerColumnName, t.ProcessPickerColumnId, t.ProcessPickerColumnEntry, t.ProcessPickerColumnRuntime]);
        var table = ui.Table(listId, ui.State.ProcessColumns, browser.ProcessSelection);
        table.Header(headers);
        using var body = table.Body(browser.Filtered.Count);
        for (var i = body.First; i < body.End; i++)
        {
            var process = browser.Filtered[i];
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
    private static bool Modules(Ui<UiState> ui, ProcessBrowser browser)
    {
        return browser.Modules.Match((ui, browser),
            none: static (s, _) => NoteInstead(s.ui, s.ui.State.Strings.ProcessPickerPickProcess),
            loading: static (s, _) => NoteInstead(s.ui, s.ui.State.Strings.ProcessPickerLoading),
            loaded: static (s, loaded) => ModuleRows(s.ui, s.browser, loaded.Modules),
            unreachable: static (s, unreachable) => NoteInstead(s.ui, s.ui.State.Strings.ProcessPickerUnreachable, unreachable.Detail),
            unreadable: static (s, unreadable) => NoteInstead(s.ui, s.ui.State.Strings.ProcessPickerUnreadable, unreadable.Detail));
    }

    /// <summary>The assemblies as a table whose columns can be resized: name, and the folder or that it is
    /// only in memory. True when the picked-out ones were activated.</summary>
    private static bool ModuleRows(Ui<UiState> ui, ProcessBrowser browser, IReadOnlyList<RunningModule> modules)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var headers = ModuleHeaders.GetValue(t, static t => [t.ProcessPickerColumnAssembly, t.ProcessPickerColumnFolder]);
        var table = ui.Table(ModuleListId, ui.State.ProcessModuleColumns, browser.ModuleSelection);
        table.Header(headers);
        using var rows = table.Body(modules);
        for (var i = rows.First; i < rows.End; i++)
        {
            using var row = rows.Row(i);
            modules[i].Switch((ui, row, theme, t),
                static (drawn, onDisk) => DrawOnDisk(drawn.ui, drawn.row, drawn.theme, onDisk),
                static (drawn, inMemory) => DrawInMemory(drawn.ui, drawn.row, drawn.theme, drawn.t, inMemory));
        }
        return rows.Activated;
    }

    private static void DrawOnDisk(Ui<UiState> ui, TableRow row, UiTheme theme, RunningModule.OnDisk onDisk)
    {
        using (row.Cell(0))
        {
            NodeIcons.Draw(ui, NodeIcon.Assembly);
            ui.Text(onDisk.Name, new Box { Width = Sizing.Grow() }, color: row.Selected ? theme.SelectionText : theme.Text,
                overflow: TextOverflow.Ellipsis);
        }
        row.TextCell(1, Path.GetDirectoryName(onDisk.Path.Text) ?? "", theme.TextMuted, overflow: TextOverflow.EllipsisMiddle);
    }

    private static void DrawInMemory(Ui<UiState> ui, TableRow row, UiTheme theme, Strings t, RunningModule.InMemory inMemory)
    {
        using (row.Cell(0))
        {
            NodeIcons.Draw(ui, NodeIcon.Assembly);
            ui.Text(inMemory.Name, new Box { Width = Sizing.Grow() }, color: theme.TextMuted, overflow: TextOverflow.Ellipsis);
        }
        row.TextCell(1, t.ProcessPickerInMemory, theme.TextMuted);
    }
}
