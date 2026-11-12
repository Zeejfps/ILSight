using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;
using ZGF.KeyboardModule;
using static ILSight.Comparisons;

namespace ILSight.ComparisonPicking;

/// <summary>The "compare with" dialog: a filter over the open assemblies the one to compare can be compared
/// with, each with its version, framework and file; Enter, a double-click or Compare compares with the
/// selected one in a new tab.</summary>
internal static class ComparisonPickerView
{
    private const string DialogId = "compare-picker";
    private const string FilterId = "compare-picker-filter";
    private const string ListId = "compare-picker-list";

    /// <summary>The dialog picking what to compare an assembly with. Compare shows the comparison in
    /// <paramref name="navigation"/>'s tabs in this frame, of <paramref name="assemblies"/> as they are open now, and
    /// queues only the comparison's run.</summary>
    public static void Render(Ui<UiState> ui, Navigation navigation, AssemblyList assemblies)
    {
        var picker = ui.State.ComparisonPicker;
        var choice = picker.Choice;
        picker.Update(assemblies);

        var compared = choice.Compared;
        if (compared is null)
        {
            return;
        }

        var t = ui.State.Strings;
        var theme = ui.Theme;
        var compare = false;
        var close = false;
        var card = new Box { Width = Sizing.Grow(0f, AppLayout.PickerWidth), Height = Sizing.Percent(AppLayout.PickerHeight) };
        var isOpen = choice.IsOpen;
        using (var dialog = ui.Dialog(DialogId, t.ComparePickerTitle(compared.NameAndVersion), ref isOpen, card))
        {
            if (!isOpen)
            {
                choice.LetGo();
            }

            if (!dialog.IsOpen)
            {
                return;
            }

            var listId = ui.GetId(ListId);
            if (!ui.HasFocus(listId))
            {
                MoveSelectionFromFilter(ui, choice, listId);
            }

            if (choice.TryTakeFilterFocus())
            {
                ui.Focus(ui.GetId(FilterId));
            }

            compare |= ui.TextField(FilterId, ref picker.Filter, t.ComparePickerFilter, clearable: true);
            using (ui.Panel(new Box { Height = Sizing.Grow(), Padding = theme.BorderWidth, Background = theme.Field, Clip = true }))
            {
                compare |= ListOrHint(ui, choice, listId);
            }

            using (ui.Row(new Box { Gap = theme.GapTight }))
            {
                ui.Spacer();
                close |= ui.Button(t.PickerCancel, id: "compare-picker-cancel");
                compare |= ui.Button(t.ComparePickerCompare, enabled: choice.Selected is not null, id: "compare-picker-compare", variant: ButtonVariant.Primary);
            }
        }
        if (compare)
        {
            close |= TryCompareWithSelected(ui.State, navigation, assemblies, choice, compared, t);
        }

        if (close)
        {
            choice.Close();
        }
    }

    /// <summary>Walks the list from the filter, leaving Home and End to its text.</summary>
    private static void MoveSelectionFromFilter(Ui<UiState> ui, ComparisonChoice choice, int listId)
    {
        var page = ui.MeasureListPage(listId, ui.Theme.RowHeight);
        var count = choice.Filtered.Count;
        foreach (var stroke in ui.Input.Keys)
        {
            var movesCaret = stroke.Key is KeyboardKey.Home or KeyboardKey.End;
            if (movesCaret)
            {
                continue;
            }

            var index = choice.Selection.Rows.Index;
            if (!UiWidgets.ApplyListKey(stroke, ref index, count, page))
            {
                continue;
            }

            choice.Selection.Rows.Select(index, count);
            ItemSelections.TakeFrom(choice.Selection, choice.Filtered);
        }
    }

    /// <summary>The assemblies offered, or why there are none. True when one was activated.</summary>
    private static bool ListOrHint(Ui<UiState> ui, ComparisonChoice choice, int listId)
    {
        if (choice.Filtered.Count > 0)
        {
            return Rows(ui, choice, listId);
        }

        var theme = ui.Theme;
        ui.Text(ui.State.Strings.ComparePickerNone, new Box { Padding = new Spacing(theme.ShellPaddingX, theme.Gap) }, color: theme.TextMuted, wrap: true);
        return false;
    }

    private static bool Rows(Ui<UiState> ui, ComparisonChoice choice, int listId)
    {
        bool activated;
        ItemSelections.ShowIn(choice.Selection, choice.Filtered);
        using (var list = ui.SelectableList(listId, choice.Filtered.Count, choice.Selection.Rows))
        {
            for (var i = list.First; i < list.End; i++)
            {
                Row(ui, list, i, choice.Filtered[i]);
            }

            activated = list.Activated;
        }
        ItemSelections.TakeFrom(choice.Selection, choice.Filtered);
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
        if (moniker is null)
        {
            return;
        }

        ui.Text(moniker, color: ui.Theme.TextMuted, size: ui.Theme.FontSizeSmall);
    }

    /// <summary>Compares the assembly to compare with the one selected, if any; true when it did.</summary>
    private static bool TryCompareWithSelected(UiState screen, Navigation navigation, AssemblyList assemblies, ComparisonChoice choice, LoadedModule compared,
        Strings t)
    {
        var other = choice.Selected;
        if (other is null)
        {
            return false;
        }

        Commands.CompareLater(screen, CompareAssemblies(navigation, assemblies, compared, other, t.ComparisonTitle(compared.Name)));
        return true;
    }
}
