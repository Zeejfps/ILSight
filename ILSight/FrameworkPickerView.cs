using System.Diagnostics;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;
using ZGF.KeyboardModule;
using static ILSight.Assemblies;

namespace ILSight;

internal static class FrameworkPickerView
{
    /// <summary>The picked-out assemblies, in the order they are shown.</summary>
    public static List<RuntimeAssembly> ListSelected(FrameworkPicker picker)
    {
        return picker.Selection.InOrder(picker.Filtered);
    }

    /// <summary>Scans the runtime the first time it opens, and again after a scan failed.</summary>
    public static void Open(FrameworkPicker picker)
    {
        Background.Start(OpenAsync(picker), picker.ReportError);
    }

    private static async Task OpenAsync(FrameworkPicker picker)
    {
        picker.IsOpen = true;
        picker.FocusFilter = true;
        picker.AppliedFilter = null;
        if (picker.Scan is RuntimeScan.Scanned or RuntimeScan.Scanning)
            return;
        picker.Scan = new RuntimeScan.Scanning();
        var scanned = await Background.Run(picker.ListAssemblies);
        picker.AppliedFilter = null;
        picker.Scan = ReportScan(picker, scanned);
    }

    private static RuntimeScan ReportScan(FrameworkPicker picker, Outcome<IReadOnlyList<RuntimeAssembly>> scanned)
    {
        switch (scanned)
        {
            case Outcome<IReadOnlyList<RuntimeAssembly>>.Ok ok:
                return new RuntimeScan.Scanned(ok.Value);
            case Outcome<IReadOnlyList<RuntimeAssembly>>.Failed failed:
                picker.ReportError(new AppProblem.RuntimeNotScanned(failed.Error));
                return new RuntimeScan.Failed(failed.Error.ToString());
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>True once after <see cref="Open"/>: the filter box should take focus.</summary>
    private static bool TakeFilterFocus(FrameworkPicker picker)
    {
        var focus = picker.FocusFilter;
        picker.FocusFilter = false;
        return focus;
    }

    /// <summary>Re-filters when the assembly list or the filter text changed since the last frame.</summary>
    public static void Update(FrameworkPicker picker, AssemblyList assemblies)
    {
        RefreshListed(picker, assemblies);
        ApplyFilter(picker);
    }

    private static void RefreshListed(FrameworkPicker picker, AssemblyList assemblies)
    {
        if (assemblies.Version == picker.ListedVersion)
            return;
        picker.ListedVersion = assemblies.Version;
        picker.Listed.Clear();
        picker.Listed.UnionWith(AssemblyListing.ListFilePaths(assemblies));
        picker.AppliedFilter = null;
    }

    private static void ApplyFilter(FrameworkPicker picker)
    {
        if (picker.Scan is not RuntimeScan.Scanned scanned)
            return;
        if (picker.Filter == picker.AppliedFilter)
            return;
        picker.AppliedFilter = picker.Filter;
        picker.Filtered.Clear();
        var filter = picker.Filter.Trim();
        foreach (var assembly in scanned.Assemblies)
        {
            if (Offers(picker, assembly, filter))
                picker.Filtered.Add(assembly);
        }
        picker.Selection.Reconcile(picker.Filtered);
        if (!picker.HasSelection)
            picker.Selection.MoveTo(0, extend: false, picker.Filtered);
    }

    /// <summary>Whether <paramref name="assembly"/> is not open yet and its name matches
    /// <paramref name="filter"/>.</summary>
    private static bool Offers(FrameworkPicker picker, RuntimeAssembly assembly, string filter)
    {
        if (picker.Listed.Contains(assembly.FilePath))
            return false;
        if (filter.Length == 0)
            return true;
        return assembly.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    public static void Render(Ui<UiState> ui, AssemblyList assemblies)
    {
        var t = ui.State.Strings;
        var picker = ui.State.Picker;
        if (picker.IsOpen)
            Update(picker, assemblies);

        var theme = ui.Theme;
        var open = false;
        var close = false;
        var card = new Box { Width = Sizing.Grow(0f, AppLayout.PickerWidth), Height = Sizing.Percent(AppLayout.PickerHeight) };
        using (var dialog = ui.Dialog("picker", t.PickerTitle, ref picker.IsOpen, card))
        {
            if (!dialog.IsOpen) return;

            var listId = ui.GetId("picker-list");
            if (!ui.HasFocus(listId))
                MoveSelectionFromFilter(ui, picker, listId);

            if (TakeFilterFocus(picker))
                ui.Focus(ui.GetId("picker-filter"));
            open |= ui.TextField("picker-filter", ref picker.Filter, t.PickerFilter);

            using (ui.Panel(new Box { Height = Sizing.Grow(), Padding = theme.BorderWidth, Background = theme.Field, Clip = true }))
            {
                switch (picker.Scan)
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
                        open |= Rows(ui, picker, listId);
                        break;
                    default:
                        throw new UnreachableException();
                }
            }

            using (ui.Row(new Box { Gap = theme.GapTight }))
            {
                var counts = (Shown: picker.Filtered.Count, Selected: picker.Selection.Count, t);
                var summary = ui.Memo("picker-count", counts, static a => Summary(a.t, a.Shown, a.Selected));
                ui.Text(summary, color: theme.TextMuted, size: theme.FontSizeSmall);
                ui.Spacer();
                close |= ui.Button(t.PickerCancel, id: "picker-cancel");
                open |= ui.Button(t.PickerOpen, enabled: picker.HasSelection, id: "picker-open", variant: ButtonVariant.Primary);
            }
        }

        if (open && picker.HasSelection)
        {
            ui.State.Later(ListSelected(picker), static (a, selected) => OpenSelected(a, selected));
            close = true;
        }
        if (close)
            picker.IsOpen = false;
    }

    private static string Summary(Strings t, int shown, int selected) => $"{t.PickerCount(shown)} · {t.PickerSelected(selected)}";

    private static void OpenSelected(AppState appState, List<RuntimeAssembly> selected)
    {
        var paths = selected
            .Select(a => a.FilePath);
        OpenAssemblies(appState, paths);
    }

    /// <summary>Walks the list from the filter, leaving Home and End to its text.</summary>
    private static void MoveSelectionFromFilter(Ui<UiState> ui, FrameworkPicker picker, int listId)
    {
        var page = ui.MeasureListPage(listId, ui.Theme.RowHeight);
        foreach (var stroke in ui.Input.Keys)
        {
            var movesCaret = stroke.Key is KeyboardKey.Home or KeyboardKey.End;
            if (!movesCaret)
                UiWidgets.ApplyListKey(stroke, picker.Selection, picker.Filtered, page);
        }
    }

    private static bool Rows(Ui<UiState> ui, FrameworkPicker picker, int listId)
    {
        var theme = ui.Theme;
        using var list = ui.SelectableList(listId, picker.Filtered, picker.Selection);
        for (var i = list.First; i < list.End; i++)
        {
            var assembly = picker.Filtered[i];
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
