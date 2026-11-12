using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.Commanding;
using static ILSight.AssemblyListArea.AssemblyLists;

namespace ILSight.AssemblyListArea;

/// <summary>
/// The named assembly lists, the active one marked with a check, each with how many assemblies it
/// holds and a button that activates it. Above them, New list and, for the picked one, Duplicate,
/// Rename and Delete; the same are in a row's context menu. A list is renamed in place, by
/// double-clicking it or pressing F2: Enter or clicking away keeps the name, Escape drops it. Delete
/// is confirmed in a dialog of its own, <see cref="AssemblyListDeleteDialogView"/>. Below them, a menu
/// adds a list of the assemblies of a framework installed here.
/// </summary>
internal static class AssemblyListsDialogView
{
    private const string ListId = "lists";
    private const string NameId = "list-name";
    private const string MenuId = "lists-menu";
    private const string PresetMenuId = "presets";

    /// <summary>The dialog over the named lists. A list activated is switched to in this frame, over
    /// <paramref name="session"/>, the documents of the one left kept docked as <paramref name="view"/> says
    /// (<see cref="AssemblyLists.SwitchAssemblyList"/>); only opening its files is queued.</summary>
    public static void Render(Ui<UiState> ui, SavedAssemblyLists lists, OpenListSession session, ViewSettings view)
    {
        var t = ui.State.Strings;
        var dialog = ui.State.ListsDialog;
        if (!dialog.IsOpen)
        {
            return;
        }

        var card = new Box { Width = Sizing.Grow(0f, AppLayout.ListsDialogWidth) };
        var open = true;
        using (var scope = ui.Dialog("assembly-lists", t.ListsTitle, ref open, card, closeOnEscape: !dialog.IsAsking))
        {
            if (!scope.IsOpen)
            {
                return;
            }

            Toolbar(ui, lists);
            Rows(ui, lists, session, view);
            RenameProblem(ui, lists);
            ContextMenu(ui, lists, session, view);
            Footer(ui);
        }
        if (!open)
        {
            AssemblyLists.CloseListsDialog(dialog);
        }
    }

    /// <summary>New list on the left; what can be done to the picked list on the right.</summary>
    private static void Toolbar(Ui<UiState> ui, SavedAssemblyLists lists)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var dialog = ui.State.ListsDialog;
        var picked = AssemblyLists.FindPickedList(dialog, lists);
        var any = picked is not null;
        using (ui.Row(new Box { Width = Sizing.Grow(), Gap = theme.GapTight, AlignY = Align.Center }))
        {
            if (ui.Button(t.ListsNew, id: "new", icon: new UiIcon(ui.State.Icons.ListsNew)))
            {
                Commands.Later(ui.State, (dialog, name: t.ListsNewName), static (a, s) => Create(s.dialog, a, s.name));
            }

            ui.Spacer();
            if (ui.IconButton("duplicate", ui.State.Icons.ListsDuplicate, t.ListsDuplicate, any) && picked is not null)
            {
                Commands.Later(ui.State, (dialog, name: picked, copyName: t.ListsCopyName(picked)), static (a, s) => Duplicate(s.dialog, a, s.name, s.copyName));
            }

            if (ui.IconButton("rename", ui.State.Icons.ListsRename, t.ListsRename, any) && picked is not null)
            {
                AssemblyLists.StartRenaming(dialog, picked);
            }

            if (ui.IconButton("delete", ui.State.Icons.ListsDelete, t.ListsDelete, any) && picked is not null)
            {
                AssemblyLists.StartConfirmingDelete(dialog, picked);
            }
        }
    }

    /// <summary>A row per list. Enter activates the picked one; a double-click renames it, as F2 does,
    /// and Delete asks to delete it. Picking another row keeps the name being typed, if it may be kept.</summary>
    private static void Rows(Ui<UiState> ui, SavedAssemblyLists lists, OpenListSession session, ViewSettings view)
    {
        var theme = ui.Theme;
        var dialog = ui.State.ListsDialog;
        var names = lists.Names;
        var picked = dialog.Selection.Index;
        var rowHeight = theme.ControlHeight + theme.GapTight;
        var frame = new Box
        {
            Height = AppLayout.ListsDialogListHeight,
            Padding = theme.BorderWidth,
            Background = theme.Field,
            BorderColor = theme.Border,
            BorderWidth = theme.BorderWidth,
            Radius = theme.RadiusSmall,
            Clip = true,
        };
        using (ui.Panel(frame))
        {
            var listId = ui.GetId(ListId);
            using var list = ui.SelectableList(listId, names.Count, dialog.Selection, rowHeight);
            var doubleClicked = false;
            for (var i = list.First; i < list.End; i++)
            {
                doubleClicked |= Row(ui, lists, session, view, list, listId, i, names[i]);
            }

            if (list.Focused)
            {
                HandleKeys(ui, lists);
            }

            if (list.Activated)
            {
                ActOnRow(ui, lists, session, view, names[list.Index], doubleClicked);
            }

            if (list.ContextMenuRequested)
            {
                ui.OpenContextMenu(MenuId, list.ContextMenuAt);
            }
        }
        if (dialog.Selection.Index != picked)
        {
            LeavePickedRow(ui);
        }
    }

    /// <summary>A check mark when the list is the active one, its name, or the box renaming it, how
    /// many assemblies it holds, and its Activate button, disabled on the active list. True when the
    /// row was double-clicked.</summary>
    private static bool Row(Ui<UiState> ui, SavedAssemblyLists lists, OpenListSession session, ViewSettings view, ListScope list, int listId, int index,
        string name)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var dialog = ui.State.ListsDialog;
        var active = name == lists.Active;
        using (ui.Id(name))
        using (var row = list.Row(index, ui.GetId("row"), new Box { Gap = theme.Gap, AlignY = Align.Center }))
        {
            ActiveMark(ui, active);
            if (dialog.Mode is ListsDialogMode.Renaming renaming && renaming.List == name)
            {
                NameBox(ui, renaming, listId);
            }
            else
            {
                NameText(ui, name, row.Selected);
            }

            var count = lists.GetPaths(name).Count;
            ui.Text(t.ListsCount(count), color: theme.TextMuted, size: theme.FontSizeSmall);
            if (ui.Button(t.ListsActivate, enabled: !active, id: "activate", variant: ButtonVariant.Ghost))
            {
                Switch(ui, lists, session, view, name);
            }

            return row.Interaction.DoubleClicked;
        }
    }

    /// <summary>The column that marks the active list with a check, empty on the others.</summary>
    private static void ActiveMark(Ui<UiState> ui, bool active)
    {
        var theme = ui.Theme;
        using (ui.Box(new Box { Width = theme.IconSize, Height = Sizing.Grow() }))
        {
            if (active)
            {
                ui.Icon(new UiIcon(ui.State.Icons.ListsActive, theme.Accent));
            }
        }
    }

    private static void NameText(Ui<UiState> ui, string name, bool selected)
    {
        var theme = ui.Theme;
        var color = selected ? theme.SelectionText : theme.Text;
        ui.Text(name, new Box { Width = Sizing.Grow() }, color: color, overflow: TextOverflow.Ellipsis);
    }

    /// <summary>The name being typed for the list being renamed, kept on Enter or once focus moves
    /// elsewhere, dropped on Escape; the list takes the focus back unless it moved elsewhere.</summary>
    private static void NameBox(Ui<UiState> ui, ListsDialogMode.Renaming renaming, int listId)
    {
        var begin = TryTakeFocus(renaming);
        var result = ui.InlineEdit(NameId, ref renaming.NewName, begin, Sizing.Grow());
        if (result == InlineEditResult.Editing)
        {
            return;
        }

        var stillHere = ui.HasFocus(ui.GetId(NameId));
        FinishNameBox(ui, result);
        if (stillHere || result == InlineEditResult.Cancelled)
        {
            ui.Focus(listId, takesText: false);
        }
    }

    /// <summary>True once: the name box should take focus with the whole name selected.</summary>
    private static bool TryTakeFocus(ListsDialogMode.Renaming renaming)
    {
        var focus = renaming.IsFocusPending;
        renaming.IsFocusPending = false;
        return focus;
    }

    private static void FinishNameBox(Ui<UiState> ui, InlineEditResult result)
    {
        if (result == InlineEditResult.Committed)
        {
            FinishRenaming(ui.State);
            return;
        }
        AssemblyLists.StopAsking(ui.State.ListsDialog);
    }

    private static void HandleKeys(Ui<UiState> ui, SavedAssemblyLists lists)
    {
        var dialog = ui.State.ListsDialog;
        if (dialog.IsAsking)
        {
            return;
        }

        var picked = AssemblyLists.FindPickedList(dialog, lists);
        if (picked is null)
        {
            return;
        }

        if (ui.Input.TakeKey(KeyboardKey.F2))
        {
            AssemblyLists.StartRenaming(dialog, picked);
        }

        if (ui.IsPressed(Shortcuts.Delete))
        {
            AssemblyLists.StartConfirmingDelete(dialog, picked);
        }
    }

    /// <summary>A double-click renames the row; Enter activates it.</summary>
    private static void ActOnRow(Ui<UiState> ui, SavedAssemblyLists lists, OpenListSession session, ViewSettings view, string name, bool doubleClicked)
    {
        if (doubleClicked)
        {
            AssemblyLists.StartRenaming(ui.State.ListsDialog, name);
            return;
        }
        Switch(ui, lists, session, view, name);
    }

    /// <summary>Switches to the list named <paramref name="name"/> in this frame and queues opening its files. The rows
    /// drawn above show it as the active list next frame, which this asks for.</summary>
    private static void Switch(Ui<UiState> ui, SavedAssemblyLists lists, OpenListSession session, ViewSettings view, string name)
    {
        Commands.OpenListLater(ui.State, SwitchAssemblyList(lists, session, view, name));
        ui.RequestUpdate();
    }

    /// <summary>Keeps the name being typed when another row is picked, and drops a deletion being asked about.</summary>
    private static void LeavePickedRow(Ui<UiState> ui)
    {
        var dialog = ui.State.ListsDialog;
        if (dialog.Mode is ListsDialogMode.Renaming)
        {
            FinishRenaming(ui.State);
            return;
        }
        AssemblyLists.StopAsking(dialog);
    }

    /// <summary>Under the list, why the name typed for the list being renamed cannot be kept.</summary>
    private static void RenameProblem(Ui<UiState> ui, SavedAssemblyLists lists)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var dialog = ui.State.ListsDialog;
        if (dialog.Mode is not ListsDialogMode.Renaming renaming)
        {
            return;
        }

        var newName = renaming.NewName;
        if (lists.CanRename(renaming.List, newName))
        {
            return;
        }

        ui.Text(DescribeRenameProblem(t, newName), color: theme.Danger, size: theme.FontSizeSmall, wrap: true);
    }

    private static string DescribeRenameProblem(Strings t, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return t.ListsNameEmpty;
        }

        return t.ListsNameTaken;
    }

    private static void ContextMenu(Ui<UiState> ui, SavedAssemblyLists lists, OpenListSession session, ViewSettings view)
    {
        using var menu = ui.ContextMenu(MenuId);
        if (!menu.IsOpen)
        {
            return;
        }

        var picked = AssemblyLists.FindPickedList(ui.State.ListsDialog, lists);
        if (picked is null)
        {
            return;
        }

        MenuItems(ui, lists, session, view, picked);
    }

    private static void MenuItems(Ui<UiState> ui, SavedAssemblyLists lists, OpenListSession session, ViewSettings view, string picked)
    {
        var t = ui.State.Strings;
        var dialog = ui.State.ListsDialog;
        var active = picked == lists.Active;
        if (ui.MenuItem(t.ListsActivate, enabled: !active, id: "activate"))
        {
            Switch(ui, lists, session, view, picked);
        }

        if (ui.MenuItem(t.ListsRename, Shortcuts.Rename, id: "rename"))
        {
            AssemblyLists.StartRenaming(dialog, picked);
        }

        if (ui.MenuItem(t.ListsDuplicate, id: "duplicate"))
        {
            Commands.Later(ui.State, (dialog, name: picked, copyName: t.ListsCopyName(picked)), static (a, s) => Duplicate(s.dialog, a, s.name, s.copyName));
        }

        ui.MenuSeparator();
        if (ui.MenuItem(t.ListsDelete, Shortcuts.Delete, id: "delete"))
        {
            AssemblyLists.StartConfirmingDelete(dialog, picked);
        }
    }

    /// <summary>Add preconfigured on the left, Close on the right.</summary>
    private static void Footer(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        var dialog = ui.State.ListsDialog;
        using (ui.DialogActions())
        {
            PresetMenu(ui);
            ui.Spacer();
            if (ui.Button(t.ListsClose, id: "close"))
            {
                AssemblyLists.CloseListsDialog(dialog);
            }
        }
    }

    /// <summary>The frameworks installed here, each with how many assemblies it holds; picking one adds
    /// a list of them, picked to be named. The frameworks are looked for the first time the menu opens, by the frame
    /// edge right after this draw, and the menu says so until they come.</summary>
    private static void PresetMenu(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        using var menu = ui.SelectButton(PresetMenuId, t.ListsAddPreset);
        if (!menu.IsOpen)
        {
            return;
        }

        var scan = ui.State.Presets;
        var presets = scan.Found;
        if (presets is null)
        {
            scan.AskForPresets();
            ui.MenuItem(t.ListsLookingForPresets, enabled: false);
            return;
        }

        if (presets.Count == 0)
        {
            ui.MenuItem(t.ListsNoPresets, enabled: false);
        }

        for (var presetIndex = 0; presetIndex < presets.Count; presetIndex++)
        {
            var preset = presets[presetIndex];
            var count = t.ListsCount(preset.Paths.Count);
            if (ui.MenuItem(preset.Name, detail: count))
            {
                Commands.Later(ui.State, (dialog: ui.State.ListsDialog, preset), static (a, s) => AddPreset(s.dialog, a, s.preset));
            }
        }
    }

    /// <summary>Adds a list of <paramref name="preset"/>'s assemblies, picked to be named, leaving the
    /// open one open.</summary>
    private static void AddPreset(AssemblyListsDialog dialog, AppState appState, AssemblyListPreset preset)
    {
        var name = AddAssemblyListPreset(appState, preset);
        AssemblyLists.PickToRename(dialog, appState.Lists, name);
    }

    /// <summary>Opens the dialog with the open list picked, its assemblies counted as they are now.</summary>
    internal static void Open(AssemblyListsDialog dialog, AppState appState)
    {
        RememberOpenAssemblies(appState.Lists, appState.ListSession);
        AssemblyLists.OpenListsDialog(dialog, appState.Lists);
    }

    /// <summary>Adds an empty list, picked to be named, leaving the open one open.</summary>
    private static void Create(AssemblyListsDialog dialog, AppState appState, string newName)
    {
        var name = CreateAssemblyList(appState, newName);
        AssemblyLists.PickToRename(dialog, appState.Lists, name);
    }

    /// <summary>Adds a copy of the list named <paramref name="name"/>, picked to be named, leaving the
    /// open one open.</summary>
    private static void Duplicate(AssemblyListsDialog dialog, AppState appState, string name, string copyName)
    {
        var copy = DuplicateAssemblyList(appState, name, copyName);
        AssemblyLists.PickToRename(dialog, appState.Lists, copy);
    }

    /// <summary>Gives the list being renamed the name typed, when it may have it, and stops renaming either way.</summary>
    private static void FinishRenaming(UiState screen)
    {
        var dialog = screen.ListsDialog;
        if (dialog.Mode is not ListsDialogMode.Renaming renaming)
        {
            return;
        }

        AssemblyLists.StopAsking(dialog);
        Commands.Later(screen, (list: renaming.List, newName: renaming.NewName), static (a, s) => a.Lists.TryRename(s.list, s.newName));
    }
}
