using System.Globalization;
using ZGF.Desktop;
using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;

namespace ILSight.ExportArea;

/// <summary>Every export of the session, as a list of downloads is: the running one with its bar and
/// a cancel button, the waiting ones, which can be taken out of the queue, and the finished ones,
/// newest first, each with its folder and report. A row is selected by clicking it or with the
/// arrow keys; Enter or a double-click opens a finished export's report, Delete takes a row off,
/// Ctrl+C copies its folder, and right-click or Shift+F10 opens its menu. Each row keeps its
/// identity, the export's run, whatever state the export is in. Narrower than
/// <see cref="NarrowWidth"/>, the actions become icons.</summary>
internal static class ExportsPanelView
{
    private const string ListKey = "exports";
    private const string MenuId = "export-menu";
    private const float NarrowWidth = 400f;
    private const float PercentWidth = 40f;
    private const int SectionQueuedId = -1;
    private const int SectionFinishedId = -2;
    private const int RowSalt = 0x45585052;
    private const int PageRows = 5;

    private readonly record struct Rows
    {
        public required int ListId { get; init; }

        // Resolved outside the rows' id scopes, so every row opens the same menu.
        public required int MenuId { get; init; }

        public required bool Narrow { get; init; }
    }

    public static void Render(Ui<UiState> ui, ExportQueue queue, List<ExportReport> finished)
    {
        var t = ui.State.Strings;
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        Toolbar(ui, queue, finished);
        ui.Separator();
        if (IsEmpty(queue, finished))
        {
            DocumentsView.Hint(ui, t.ExportsHint);
            return;
        }
        var listId = ui.GetId(ListKey);
        var rows = new Rows { ListId = listId, MenuId = ui.GetId(MenuId), Narrow = IsNarrow(ui, listId) };
        HandleKeys(ui, queue, finished, rows);
        using (ui.Scroll(rows.ListId, new Box { Height = Sizing.Grow(), Gap = 0f, Background = ui.Theme.PaneBackground }))
        {
            Running(ui, queue, rows);
            Waiting(ui, queue, rows);
            Finished(ui, queue, finished, rows);
        }
        Menu(ui, queue, finished, rows);
    }

    private static bool IsEmpty(ExportQueue export, List<ExportReport> finished)
    {
        if (export.IsRunning)
        {
            return false;
        }

        return finished.Count == 0;
    }

    // Measured on the last frame: the list's width this frame is not known yet.
    private static bool IsNarrow(Ui<UiState> ui, int listId)
    {
        if (!ui.TryGetRect(listId, out var rect))
        {
            return false;
        }

        return rect.Width < NarrowWidth;
    }

    private static void Toolbar(Ui<UiState> ui, ExportQueue export, List<ExportReport> finished)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        using (ui.PaneToolbar(new Box { Padding = new Spacing(theme.PanelPadding, theme.ToolbarPaddingY) }))
        {
            var summary = ui.Memo("summary", (export.IsRunning, export.WaitingCount, finished.Count, t),
                static a => Summarize(a.t, a.IsRunning, a.WaitingCount, a.Count));
            ui.Text(summary, color: theme.TextMuted, size: theme.FontSizeSmall);
            ui.Spacer();
            if (ui.Button(t.ExportsClearFinished, enabled: finished.Count > 0, id: "clear", variant: ButtonVariant.Ghost))
            {
                Commands.Later(ui.State, static a => Exports.ClearFinishedExports(a));
            }

            ui.Tooltip(ui.GetId("clear"), t.ExportsClearFinishedTooltip);
        }
    }

    private static string Summarize(Strings t, bool running, int waiting, int finished)
    {
        var parts = new List<string>();
        if (running)
        {
            parts.Add(t.ExportsSummaryRunning);
        }

        if (waiting > 0)
        {
            parts.Add(t.ExportsSummaryQueued(waiting));
        }

        if (finished > 0)
        {
            parts.Add(t.ExportsSummaryFinished(finished));
        }

        return string.Join(" · ", parts);
    }

    private static void HandleKeys(Ui<UiState> ui, ExportQueue queue, List<ExportReport> finished, Rows rows)
    {
        if (!ui.HasFocus(rows.ListId))
        {
            return;
        }

        var runs = Exports.ListExports(queue, finished);
        var keys = ui.Input.Keys;
        for (var i = 0; i < keys.Count; i++)
        {
            HandleKey(ui, queue, finished, rows, runs, i);
        }
    }

    private static void HandleKey(Ui<UiState> ui, ExportQueue queue, List<ExportReport> finished, Rows rows, List<ExportRun> runs, int index)
    {
        var stroke = ui.Input.Keys[index];
        var selected = ui.State.SelectedExport;
        if (selected is not null && ActOnSelected(ui, queue, finished, rows, selected, stroke, index))
        {
            return;
        }

        MoveSelection(ui, rows, runs, stroke);
    }

    private static bool ActOnSelected(Ui<UiState> ui, ExportQueue queue, List<ExportReport> finished, Rows rows, ExportRun selected, KeyStroke stroke, int index)
    {
        if (stroke.Key == KeyboardKey.Menu || ui.IsPressedAt(Shortcuts.ContextMenu, index))
        {
            OpenMenuOnSelection(ui, rows, selected);
            return true;
        }
        if (stroke.Key == KeyboardKey.Enter)
        {
            Commands.Later(ui.State, selected, static (a, run) => Exports.ActivateExport(a, run));
            return true;
        }
        if (ui.IsPressedAt(Shortcuts.Copy, index))
        {
            CopyFolder(ui, queue, finished, selected);
            return true;
        }
        if (stroke.Key == KeyboardKey.Delete)
        {
            Commands.Later(ui.State, selected, static (a, run) => Exports.RemoveExport(a, run));
            return true;
        }
        return false;
    }

    private static void MoveSelection(Ui<UiState> ui, Rows rows, List<ExportRun> runs, KeyStroke stroke)
    {
        var selected = ui.State.SelectedExport;
        var index = selected is null ? -1 : runs.IndexOf(selected);
        var before = index;
        UiWidgets.ApplyListKey(stroke, ref index, runs.Count, PageRows);
        if (index == before || index < 0)
        {
            return;
        }

        var moved = runs[index];
        ui.State.SelectedExport = moved;
        using (ui.Id(RowIdOf(moved)))
        {
            ui.ScrollIntoView(rows.ListId, ui.GetId("row"));
        }
    }

    // Salted to keep the rows' ids apart from the sections' ids.
    private static int RowIdOf(ExportRun run)
    {
        return HashCode.Combine(run.Number, RowSalt);
    }

    private static void CopyFolder(Ui<UiState> ui, ExportQueue queue, List<ExportReport> finished, ExportRun run)
    {
        var folder = Exports.FindExportFolder(queue, finished, run);
        if (folder is null)
        {
            return;
        }

        ui.ClipboardText = folder;
    }

    private static void OpenMenuOnSelection(Ui<UiState> ui, Rows rows, ExportRun run)
    {
        PointF? at = null;
        using (ui.Id(RowIdOf(run)))
        {
            var rowId = ui.GetId("row");
            if (ui.TryGetRect(rowId, out var rect))
            {
                at = new PointF(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            }
        }
        OpenMenu(ui, rows, run, at);
    }

    private static void OpenMenu(Ui<UiState> ui, Rows rows, ExportRun run, PointF? at)
    {
        ui.State.ExportMenuTarget = run;
        ui.OpenContextMenu(rows.MenuId, at);
    }

    private static Interaction RowHit(Ui<UiState> ui, Rows rows, ExportRun run)
    {
        var hit = ui.Interact(ui.GetId("row"));
        if (hit.Pressed)
        {
            ui.State.SelectedExport = run;
            ui.Focus(rows.ListId, takesText: false);
        }
        if (hit.DoubleClicked)
        {
            Commands.Later(ui.State, run, static (a, r) => Exports.ActivateExport(a, r));
        }

        if (hit.ClickedWith(PointerButton.Right))
        {
            ui.State.SelectedExport = run;
            OpenMenu(ui, rows, run, ui.Input.Pointer);
        }
        return hit;
    }

    private static uint RowBackground(Ui<UiState> ui, Rows rows, ExportRun run, Interaction hit)
    {
        var theme = ui.Theme;
        if (ui.State.SelectedExport == run)
        {
            return ui.HasFocus(rows.ListId) ? theme.Selection : theme.SelectionInactive;
        }

        if (hit.Hovered)
        {
            return theme.RowHover;
        }

        return 0u;
    }

    private static void Running(Ui<UiState> ui, ExportQueue export, Rows rows)
    {
        var running = export.Running;
        if (running is null)
        {
            return;
        }

        var t = ui.State.Strings;
        var theme = ui.Theme;
        var queued = running.Export;
        using (ui.Id(RowIdOf(queued.Run)))
        {
            var hit = RowHit(ui, rows, queued.Run);
            using (ui.Column(hit.Id, RowBox(theme, RowBackground(ui, rows, queued.Run, hit))))
            {
                var at = running.Progress;
                using (ui.Row(TopLine(theme)))
                {
                    ui.Spinner(theme.PaneBackground);
                    Name(ui, queued.Job.Name);
                    Percent(ui, at.Fraction);
                    CancelAction(ui, rows, running.IsCancelling);
                }
                var preparing = at.Fraction is null;
                var detail = ui.Memo("detail", (queued.Job.Kind, at.Name, at.Part, at.Parts, preparing, running.IsCancelling, t),
                    static a => DescribeRunning(a.t, a.Kind, a.Name, a.Part, a.Parts, a.preparing, a.IsCancelling));
                Detail(ui, detail);
                Folder(ui, queued.Job.Folder);
                Bar(ui, at.Fraction);
            }
            ui.Tooltip(hit.Id, queued.Job.Folder);
        }
        ui.Separator();
    }

    private static void CancelAction(Ui<UiState> ui, Rows rows, bool cancelling)
    {
        var t = ui.State.Strings;
        var enabled = !cancelling;
        if (rows.Narrow)
        {
            if (ui.IconButton("cancel", ui.State.Icons.ExportCancel, t.ExportsCancelTooltip, enabled))
            {
                Commands.Later(ui.State, static a => a.Export.CancelRunning());
            }

            return;
        }
        if (ui.Button(t.ExportsCancel, enabled: enabled, id: "cancel", variant: ButtonVariant.Ghost))
        {
            Commands.Later(ui.State, static a => a.Export.CancelRunning());
        }

        ui.Tooltip(ui.GetId("cancel"), t.ExportsCancelTooltip);
    }

    private static string DescribeRunning(Strings t, ExportKind kind, string name, int part, int parts, bool preparing, bool cancelling)
    {
        var kindText = ExportReportText.KindOf(t, kind);
        if (cancelling)
        {
            return $"{kindText} · {t.ExportsCancelling}";
        }

        if (parts > 1)
        {
            return $"{kindText} · {t.ExportsAssemblyPart(part, parts, name)}";
        }

        if (preparing)
        {
            return $"{kindText} · {t.ExportsPreparing}";
        }

        return kindText;
    }

    private static void Percent(Ui<UiState> ui, float? fraction)
    {
        var theme = ui.Theme;
        var percent = ui.Memo("percent", fraction, static f => FormatPercent(f));
        // In a slot of its own, so the actions beside it stay put.
        ui.Text(percent, new Box { Width = Sizing.Fixed(PercentWidth) }, color: theme.TextMuted, size: theme.FontSizeSmall, align: Align.End);
    }

    private static void Bar(Ui<UiState> ui, float? fraction)
    {
        var theme = ui.Theme;
        using (ui.Row(new Box { Width = Sizing.Grow(), Padding = new Spacing(NameIndent(theme), theme.GapTight, 0f, 0f) }))
        {
            if (fraction is not null)
            {
                ui.ProgressBar(fraction.Value);
            }
            else
            {
                ui.SlidingBar();
            }
        }
    }

    private static string FormatPercent(float? fraction)
    {
        if (fraction is null)
        {
            return "";
        }

        var percent = (int)(fraction.Value * 100f);
        return $"{percent}%";
    }

    private static void Waiting(Ui<UiState> ui, ExportQueue export, Rows rows)
    {
        var t = ui.State.Strings;
        var count = export.WaitingCount;
        if (count > 1)
        {
            Section(ui, SectionQueuedId, ui.Memo("queued", (count, t), static a => a.t.ExportsSectionQueued(a.count)));
        }

        var ahead = 0;
        ExportRun? withdrawn = null;
        foreach (var waiting in export.Waiting)
        {
            if (WaitingRow(ui, rows, waiting, ahead))
            {
                withdrawn = waiting.Run;
            }

            ahead++;
        }
        // Taken out of the queue once it is no longer being walked.
        if (withdrawn is not null)
        {
            Commands.Later(ui.State, withdrawn, static (a, run) => a.Export.Withdraw(run));
        }
    }

    private static bool WaitingRow(Ui<UiState> ui, Rows rows, QueuedExport waiting, int ahead)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        bool withdrawn;
        using (ui.Id(RowIdOf(waiting.Run)))
        {
            var hit = RowHit(ui, rows, waiting.Run);
            using (ui.Column(hit.Id, RowBox(theme, RowBackground(ui, rows, waiting.Run, hit))))
            {
                using (ui.Row(TopLine(theme)))
                {
                    Glyph(ui, ui.State.Icons.ExportWaiting, theme.TextMuted);
                    Name(ui, waiting.Job.Name);
                    withdrawn = WithdrawAction(ui, rows);
                }
                var detail = ui.Memo("detail", (waiting.Job.Kind, ahead, t), static a => DescribeWaiting(a.t, a.Kind, a.ahead));
                Detail(ui, detail);
                Folder(ui, waiting.Job.Folder);
            }
            ui.Tooltip(hit.Id, waiting.Job.Folder);
        }
        ui.Separator();
        return withdrawn;
    }

    private static bool WithdrawAction(Ui<UiState> ui, Rows rows)
    {
        var t = ui.State.Strings;
        if (rows.Narrow)
        {
            return ui.IconButton("withdraw", ui.State.Icons.ExportWithdraw, t.ExportsWithdraw);
        }

        return ui.Button(t.ExportsWithdraw, id: "withdraw", variant: ButtonVariant.Ghost);
    }

    private static string DescribeWaiting(Strings t, ExportKind kind, int ahead)
    {
        var kindText = ExportReportText.KindOf(t, kind);
        if (ahead == 0)
        {
            return $"{kindText} · {t.ExportsUpNext}";
        }

        return $"{kindText} · {t.ExportsAhead(ahead)}";
    }

    private static void Finished(Ui<UiState> ui, ExportQueue export, List<ExportReport> reports, Rows rows)
    {
        var t = ui.State.Strings;
        if (reports.Count == 0)
        {
            return;
        }

        if (HasActiveWork(export))
        {
            Section(ui, SectionFinishedId, ui.Memo("finished", (reports.Count, t), static a => a.t.ExportsSectionFinished(a.Count)));
        }

        ExportReport? removed = null;
        for (var i = reports.Count - 1; i >= 0; i--)
        {
            if (FinishedRow(ui, rows, reports[i]))
            {
                removed = reports[i];
            }
        }
        if (removed is not null)
        {
            Commands.Later(ui.State, removed, static (a, report) => Exports.RemoveFinishedExport(a, report));
        }
    }

    private static bool HasActiveWork(ExportQueue export)
    {
        if (export.IsRunning)
        {
            return true;
        }

        return export.WaitingCount > 0;
    }

    private static bool FinishedRow(Ui<UiState> ui, Rows rows, ExportReport report)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        bool removed;
        using (ui.Id(RowIdOf(report.Run)))
        {
            var hit = RowHit(ui, rows, report.Run);
            using (ui.Column(hit.Id, RowBox(theme, RowBackground(ui, rows, report.Run, hit))))
            {
                using (ui.Row(TopLine(theme)))
                {
                    Glyph(ui, OutcomeIcon(ui.State.Icons, report), OutcomeColor(theme, report));
                    Name(ui, report.Name);
                    FinishedActions(ui, rows, report);
                    removed = ui.IconButton("remove", ui.State.Icons.ExportRemove, t.ExportsRemove);
                }
                var detail = ui.Memo("detail", (report, t), static a => DescribeFinished(a.t, a.report));
                Detail(ui, detail, OutcomeTextColor(theme, report));
                Folder(ui, report.Folder);
            }
            RecordFinishedPaths(ui, hit.Id, report.Run);
            ui.Tooltip(hit.Id, report.Folder);
        }
        ui.Separator();
        return removed;
    }

    // Has tests find a finished export's row by its number, from 1 for the first export of the session, and its buttons
    // under it, as #1/open-folder, since every row has the same buttons.
    private static void RecordFinishedPaths(Ui<UiState> ui, int rowId, ExportRun run)
    {
        if (!ui.WantsSemantics)
        {
            return;
        }

        ui.RecordTestPath(rowId, new TestSegment.Keyed(run.Number.ToString(CultureInfo.InvariantCulture)));
        foreach (var name in FinishedButtons)
        {
            ui.RecordTestPath(ui.GetId(name), new TestSegment.Named(name), under: rowId);
        }
    }

    private static readonly string[] FinishedButtons = ["open-folder", "show-report", "remove"];

    private static string DescribeFinished(Strings t, ExportReport report)
    {
        var kindText = ExportReportText.KindOf(t, report.Job.Kind);
        var outcome = ExportReportText.DescribeOutcome(t, report);
        return $"{kindText} · {outcome}";
    }

    private static void FinishedActions(Ui<UiState> ui, Rows rows, ExportReport report)
    {
        if (OpensFolderFirst(report))
        {
            OpenFolderAction(ui, report, labelled: !rows.Narrow);
            ShowReportAction(ui, report, labelled: false);
            return;
        }
        ShowReportAction(ui, report, labelled: !rows.Narrow);
        if (report.FolderExists)
        {
            OpenFolderAction(ui, report, labelled: false);
        }
    }

    private static void OpenFolderAction(Ui<UiState> ui, ExportReport report, bool labelled)
    {
        var t = ui.State.Strings;
        if (labelled && ui.Button(t.ExportOpenFolder, id: "open-folder", icon: new UiIcon(ui.State.Icons.ExportOpenFolder)))
        {
            ui.OpenFolder(report.Folder);
        }

        if (!labelled && ui.IconButton("open-folder", ui.State.Icons.ExportOpenFolder, t.ExportOpenFolder))
        {
            ui.OpenFolder(report.Folder);
        }
    }

    private static void ShowReportAction(Ui<UiState> ui, ExportReport report, bool labelled)
    {
        var t = ui.State.Strings;
        if (labelled && ui.Button(t.ExportShowReport, id: "show-report", icon: new UiIcon(ui.State.Icons.ExportShowReport)))
        {
            Commands.Later(ui.State, report, static (a, r) => Exports.ShowExportReport(a, r));
        }

        if (!labelled && ui.IconButton("show-report", ui.State.Icons.ExportShowReport, t.ExportShowReport))
        {
            Commands.Later(ui.State, report, static (a, r) => Exports.ShowExportReport(a, r));
        }
    }

    private static bool OpensFolderFirst(ExportReport report)
    {
        var isClean = report.End.Match(static finished => !finished.HasProblems, static _ => false, static _ => false);
        if (!isClean)
        {
            return false;
        }

        return report.FolderExists;
    }

    private static IconGlyph OutcomeIcon(AppIcons icons, ExportReport report)
    {
        return report.End.Match(icons,
            static (i, finished) => FinishedIcon(i, finished),
            static (i, _) => i.ExportCancelled,
            static (i, _) => i.ExportProblems);
    }

    private static IconGlyph FinishedIcon(AppIcons icons, ExportEnd.Finished finished)
    {
        if (finished.HasProblems)
        {
            return icons.ExportProblems;
        }

        return icons.ExportDone;
    }

    private static uint OutcomeColor(UiTheme theme, ExportReport report)
    {
        return report.End.Match(theme,
            static (th, finished) => FinishedColor(th, finished, clean: th.Success),
            static (th, _) => th.TextMuted,
            static (th, _) => th.Danger);
    }

    // The outcome's own color when something went wrong, so the problem reads as well as the icon shows it.
    private static uint OutcomeTextColor(UiTheme theme, ExportReport report)
    {
        return report.End.Match(theme,
            static (th, finished) => FinishedColor(th, finished, clean: th.TextMuted),
            static (th, _) => th.TextMuted,
            static (th, _) => th.Danger);
    }

    private static uint FinishedColor(UiTheme theme, ExportEnd.Finished finished, uint clean)
    {
        if (finished.HasProblems)
        {
            return theme.Warning;
        }

        return clean;
    }

    private static void Menu(Ui<UiState> ui, ExportQueue queue, List<ExportReport> finished, Rows rows)
    {
        using var menu = ui.ContextMenu(rows.MenuId);
        if (!menu.IsOpen)
        {
            return;
        }

        var run = ui.State.ExportMenuTarget;
        if (run is null)
        {
            return;
        }

        var report = Exports.FindFinishedExport(finished, run);
        if (report is not null)
        {
            FinishedMenu(ui, report);
        }
        else
        {
            QueuedMenu(ui, queue, finished, run);
        }
    }

    private static void FinishedMenu(Ui<UiState> ui, ExportReport report)
    {
        var t = ui.State.Strings;
        if (ui.MenuItem(t.ExportShowReport, Shortcuts.Enter, id: "show-report", icon: new UiIcon(ui.State.Icons.ExportShowReport)))
        {
            Commands.Later(ui.State, report, static (a, r) => Exports.ShowExportReport(a, r));
        }

        if (ui.MenuItem(t.ExportOpenFolder, enabled: report.FolderExists, id: "open-folder", icon: new UiIcon(ui.State.Icons.ExportOpenFolder)))
        {
            ui.OpenFolder(report.Folder);
        }

        if (ui.MenuItem(t.ExportsCopyFolder, Shortcuts.Copy, id: "copy-folder"))
        {
            ui.ClipboardText = report.Folder;
        }

        ui.MenuSeparator();
        if (ui.MenuItem(t.ExportsRemove, Shortcuts.Delete, id: "remove"))
        {
            Commands.Later(ui.State, report, static (a, r) => Exports.RemoveFinishedExport(a, r));
        }
    }

    private static void QueuedMenu(Ui<UiState> ui, ExportQueue queue, List<ExportReport> finished, ExportRun run)
    {
        var t = ui.State.Strings;
        var folder = Exports.FindExportFolder(queue, finished, run);
        if (folder is null)
        {
            return;
        }

        if (ui.MenuItem(t.ExportsCopyFolder, Shortcuts.Copy, id: "copy-folder"))
        {
            ui.ClipboardText = folder;
        }

        ui.MenuSeparator();
        var running = queue.Running;
        if (running is not null && running.Export.Run == run)
        {
            if (ui.MenuItem(t.ExportsCancel, enabled: !running.IsCancelling, id: "cancel"))
            {
                Commands.Later(ui.State, static a => a.Export.CancelRunning());
            }

            return;
        }
        if (ui.MenuItem(t.ExportsWithdraw, Shortcuts.Delete, id: "withdraw"))
        {
            Commands.Later(ui.State, run, static (a, r) => a.Export.Withdraw(r));
        }
    }

    private static void Section(Ui<UiState> ui, int id, string text)
    {
        var theme = ui.Theme;
        using (ui.Id(id))
        using (ui.Row(new Box { Width = Sizing.Grow(), Padding = new Spacing(theme.PanelPadding, theme.GapTight), Background = theme.Surface }))
        {
            ui.Text(text, color: theme.TextMuted, size: theme.FontSizeSmall, bold: true);
        }

        ui.Separator();
    }

    private static void Glyph(Ui<UiState> ui, IconGlyph glyph, uint color)
    {
        var theme = ui.Theme;
        ui.Glyph(glyph, color, theme.IconSize, new Box { Width = Sizing.Fixed(theme.IconSize) });
    }

    private static void Name(Ui<UiState> ui, string name)
    {
        var theme = ui.Theme;
        ui.Text(name, new Box { Width = Sizing.Grow() }, color: theme.Text, overflow: TextOverflow.Ellipsis);
    }

    private static void Detail(Ui<UiState> ui, string text, uint? color = null)
    {
        var theme = ui.Theme;
        ui.Text(text, new Box { Width = Sizing.Grow(), Padding = Indent(theme) }, color: color ?? theme.TextMuted, size: theme.FontSizeSmall,
            overflow: TextOverflow.Ellipsis);
    }

    private static void Folder(Ui<UiState> ui, string folder)
    {
        var theme = ui.Theme;
        // Cut in the middle so its last folder stays; the row's tooltip holds it whole.
        ui.Text(folder, new Box { Width = Sizing.Grow(), Padding = Indent(theme) }, color: theme.TextMuted, size: theme.FontSizeSmall,
            overflow: TextOverflow.EllipsisMiddle);
    }

    private static float NameIndent(UiTheme theme)
    {
        return theme.IconSize + theme.Gap;
    }

    private static Spacing Indent(UiTheme theme)
    {
        return new Spacing(NameIndent(theme), 0f, 0f, 0f);
    }

    private static Box RowBox(UiTheme theme, uint background)
    {
        return new Box()
        {
            Width = Sizing.Grow(),
            Gap = 0f,
            Padding = new Spacing(theme.PanelPadding, theme.GapTight, theme.PanelPadding, theme.Gap),
            Background = background,
        };
    }

    private static Box TopLine(UiTheme theme)
    {
        return new Box() { Width = Sizing.Grow(), Gap = theme.Gap, AlignY = Align.Center };
    }
}
