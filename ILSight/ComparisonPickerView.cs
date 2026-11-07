using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;
using ZGF.KeyboardModule;
using static ILSight.Comparisons;

namespace ILSight;

/// <summary>The "compare with" dialog: a filter over the open assemblies the one to compare can be compared
/// with, each with its version, framework and file; Enter, a double-click or Compare compares with the
/// selected one in a new tab.</summary>
internal static class ComparisonPickerView
{
    private const string DialogId = "compare-picker";
    private const string FilterId = "compare-picker-filter";
    private const string ListId = "compare-picker-list";

    /// <summary>Opens the dialog to pick what to compare <paramref name="compared"/> with.</summary>
    public static void Open(ComparisonPicker picker, LoadedModule compared)
    {
        picker.Dialog = new ComparisonDialog.Open(compared);
        picker.Filter = "";
        ItemSelections.Clear(picker.Selection);
        picker.FocusFilter = true;
        picker.ListedVersion = -1;
        picker.AppliedFilter = null;
    }

    private static void Close(ComparisonPicker picker)
    {
        picker.Dialog = new ComparisonDialog.Closed();
    }

    /// <summary>Has the dialog go out, as ZGF dismisses it through a <see langword="ref"/> bool: it keeps
    /// what it shows while it goes, where closing it from the buttons drops it at once.</summary>
    private static void LetGo(ComparisonPicker picker)
    {
        if (picker.Dialog is ComparisonDialog.Open open)
            picker.Dialog = new ComparisonDialog.Going(open.Compared);
    }

    /// <summary>True once after <see cref="Open"/>: the filter box should take focus.</summary>
    private static bool TakeFilterFocus(ComparisonPicker picker)
    {
        var focus = picker.FocusFilter;
        picker.FocusFilter = false;
        return focus;
    }

    /// <summary>Lists the assemblies again when the open ones or the filter changed since the last frame;
    /// closes the dialog when the assembly to compare is no longer open.</summary>
    public static void Update(ComparisonPicker picker, AssemblyList assemblies)
    {
        var compared = picker.Compared;
        if (compared == null)
            return;
        RefreshCandidates(picker, assemblies, compared);
        ApplyFilter(picker);
    }

    private static void RefreshCandidates(ComparisonPicker picker, AssemblyList assemblies, LoadedModule compared)
    {
        if (assemblies.Version == picker.ListedVersion)
            return;
        picker.ListedVersion = assemblies.Version;
        var open = assemblies.Assemblies;
        if (!open.Contains(compared))
        {
            Close(picker);
            return;
        }
        picker.Candidates.Clear();
        picker.Candidates.AddRange(ListCandidates(open, compared));
        picker.AppliedFilter = null;
    }

    private static IEnumerable<LoadedModule> ListCandidates(IReadOnlyList<LoadedModule> open, LoadedModule compared)
    {
        return open
            .Where(a => a != compared)
            .OrderByDescending(a => string.Equals(a.Name, compared.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Filters the candidates, selecting the first shown unless the one selected still is.</summary>
    private static void ApplyFilter(ComparisonPicker picker)
    {
        if (!picker.IsOpen)
            return;
        if (picker.Filter == picker.AppliedFilter)
            return;
        picker.AppliedFilter = picker.Filter;
        var selected = picker.Selected;
        picker.Filtered.Clear();
        var filter = picker.Filter.Trim();
        foreach (var candidate in picker.Candidates)
        {
            if (Matches(candidate, filter))
                picker.Filtered.Add(candidate);
        }
        SelectAgain(picker, selected);
    }

    private static bool Matches(LoadedModule assembly, string filter)
    {
        if (filter.Length == 0)
            return true;
        if (assembly.NameAndVersion.Contains(filter, StringComparison.OrdinalIgnoreCase))
            return true;
        return assembly.FilePath.Text.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private static void SelectAgain(ComparisonPicker picker, LoadedModule? selected)
    {
        if (picker.Filtered.Count == 0)
        {
            ItemSelections.Clear(picker.Selection);
            return;
        }
        var index = 0;
        if (selected != null)
            index = Math.Max(0, picker.Filtered.IndexOf(selected));
        ItemSelections.Select(picker.Selection, picker.Filtered[index], picker.Filtered);
    }

    public static void Render(Ui<UiState> ui, AssemblyList assemblies)
    {
        var picker = ui.State.ComparisonPicker;
        if (picker.IsOpen)
            Update(picker, assemblies);
        var compared = picker.Compared;
        if (compared == null)
            return;
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var compare = false;
        var close = false;
        var card = new Box { Width = Sizing.Grow(0f, AppLayout.PickerWidth), Height = Sizing.Percent(AppLayout.PickerHeight) };
        var isOpen = picker.IsOpen;
        using (var dialog = ui.Dialog(DialogId, t.ComparePickerTitle(compared.NameAndVersion), ref isOpen, card))
        {
            if (!isOpen)
                LetGo(picker);
            if (!dialog.IsOpen)
                return;
            var listId = ui.GetId(ListId);
            if (!ui.HasFocus(listId))
                MoveSelectionFromFilter(ui, picker, listId);
            if (TakeFilterFocus(picker))
                ui.Focus(ui.GetId(FilterId));
            compare |= ui.TextField(FilterId, ref picker.Filter, t.ComparePickerFilter);
            using (ui.Panel(new Box { Height = Sizing.Grow(), Padding = theme.BorderWidth, Background = theme.Field, Clip = true }))
                compare |= ListOrHint(ui, picker, listId);
            using (ui.Row(new Box { Gap = theme.GapTight }))
            {
                ui.Spacer();
                close |= ui.Button(t.PickerCancel, id: "compare-picker-cancel");
                compare |= ui.Button(t.ComparePickerCompare, enabled: picker.Selected != null, id: "compare-picker-compare", variant: ButtonVariant.Primary);
            }
        }
        if (compare)
            close |= CompareWithSelected(ui.State, picker, compared, t);
        if (close)
            Close(picker);
    }

    /// <summary>Walks the list from the filter, leaving Home and End to its text.</summary>
    private static void MoveSelectionFromFilter(Ui<UiState> ui, ComparisonPicker picker, int listId)
    {
        var page = ui.MeasureListPage(listId, ui.Theme.RowHeight);
        var count = picker.Filtered.Count;
        foreach (var stroke in ui.Input.Keys)
        {
            var movesCaret = stroke.Key is KeyboardKey.Home or KeyboardKey.End;
            if (movesCaret)
                continue;
            var index = picker.Selection.Rows.Index;
            if (!UiWidgets.ApplyListKey(stroke, ref index, count, page))
                continue;
            picker.Selection.Rows.Select(index, count);
            ItemSelections.TakeFrom(picker.Selection, picker.Filtered);
        }
    }

    /// <summary>The assemblies offered, or why there are none. True when one was activated.</summary>
    private static bool ListOrHint(Ui<UiState> ui, ComparisonPicker picker, int listId)
    {
        if (picker.Filtered.Count > 0)
            return Rows(ui, picker, listId);
        var theme = ui.Theme;
        ui.Text(ui.State.Strings.ComparePickerNone, new Box { Padding = new Spacing(theme.ShellPaddingX, theme.Gap) }, color: theme.TextMuted, wrap: true);
        return false;
    }

    private static bool Rows(Ui<UiState> ui, ComparisonPicker picker, int listId)
    {
        bool activated;
        ItemSelections.ShowIn(picker.Selection, picker.Filtered);
        using (var list = ui.SelectableList(listId, picker.Filtered.Count, picker.Selection.Rows))
        {
            for (var i = list.First; i < list.End; i++)
                Row(ui, list, i, picker.Filtered[i]);
            activated = list.Activated;
        }
        ItemSelections.TakeFrom(picker.Selection, picker.Filtered);
        return activated;
    }

    /// <summary>The assembly's name and version, then its framework and, muted, its file, shortened in the middle so its folder and name stay.</summary>
    private static void Row(Ui<UiState> ui, ListScope list, int index, LoadedModule assembly)
    {
        var theme = ui.Theme;
        using (ui.Id(assembly.FilePath.Text))
        using (var row = list.Row(index, ui.GetId("row"), new Box { Gap = theme.GapTight, AlignY = Align.Center }))
        {
            var nameColor = row.Selected ? theme.SelectionText : theme.Text;
            NodeIcons.Draw(ui, NodeIcon.Assembly);
            ui.Text(assembly.NameAndVersion, color: nameColor);
            FrameworkText(ui, assembly);
            ui.Text(assembly.FilePath.Text, new Box { Width = Sizing.Grow() }, color: theme.TextMuted, size: theme.FontSizeSmall, overflow: TextOverflow.EllipsisMiddle);
        }
    }

    private static void FrameworkText(Ui<UiState> ui, LoadedModule assembly)
    {
        var moniker = assembly.FrameworkMoniker;
        if (moniker == null)
            return;
        ui.Text(moniker, color: ui.Theme.TextMuted, size: ui.Theme.FontSizeSmall);
    }

    /// <summary>Compares the assembly to compare with the one selected, if any; true when it did.</summary>
    private static bool CompareWithSelected(UiState screen, ComparisonPicker picker, LoadedModule compared, Strings t)
    {
        var other = picker.Selected;
        if (other == null)
            return false;
        Commands.Later(screen, (compared, other, title: t.ComparisonTitle(compared.Name)), static (a, s) => CompareAssemblies(a, s.compared, s.other, s.title));
        return true;
    }
}
