using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.CommandArea;

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
        var t = ui.State.Appearance.Strings;
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

            Toolbar(ui, ui.State.Appearance, ui.State.ListsDialog, ui.State.AppCommands, lists);
            Rows(ui, lists, session, view);
            RenameProblem(ui, ui.State.Appearance, ui.State.ListsDialog, lists);
            ContextMenu(ui, ui.State.ListsDialog, ui.State.Appearance, ui.State.AppCommands, lists, session, view);
            Footer(ui, ui.State.Appearance, ui.State.ListsDialog, ui.State.Presets, ui.State.AppCommands);
        }
        if (!open)
        {
            dialog.Close();
        }
    }

    private static void Toolbar(Ui ui, Appearance appearance, AssemblyListsDialog listsDialog, IAppCommandWriter commands, SavedAssemblyLists lists)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var dialog = listsDialog;
        var picked = dialog.FindPickedList(lists);
        var any = picked is not null;
        using (ui.Row(new Box { Width = Sizing.Grow(), Gap = theme.GapTight, AlignY = Align.Center }))
        {
            if (ui.Button(t.ListsNew, id: "new", icon: new UiIcon(appearance.Icons.ListsNew)))
            {
                Commands.Later(commands, (dialog, name: t.ListsNewName), static (a, s) => Create(s.dialog, a, s.name));
            }

            ui.Spacer();
            if (ui.IconButton("duplicate", appearance.Icons.ListsDuplicate, t.ListsDuplicate, any) && picked is not null)
            {
                Commands.Later(commands, (dialog, name: picked, copyName: t.ListsCopyName(picked)), static (a, s) => Duplicate(s.dialog, a, s.name, s.copyName));
            }

            if (ui.IconButton("rename", appearance.Icons.ListsRename, t.ListsRename, any) && picked is not null)
            {
                dialog.StartRenaming(picked);
            }

            if (ui.IconButton("delete", appearance.Icons.ListsDelete, t.ListsDelete, any) && picked is not null)
            {
                dialog.StartConfirmingDelete(picked);
            }
        }
    }

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
                HandleKeys(ui, ui.State.ListsDialog, lists);
            }

            if (list.Activated)
            {
                ActOnRow(ui, ui.State.ListsDialog, ui.State.AppCommands, lists, session, view, names[list.Index], doubleClicked);
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

    private static bool Row(Ui<UiState> ui, SavedAssemblyLists lists, OpenListSession session, ViewSettings view, ListScope list, int listId, int index,
        string name)
    {
        var t = ui.State.Appearance.Strings;
        var theme = ui.Theme;
        var dialog = ui.State.ListsDialog;
        var active = name == lists.Active;
        using (ui.Id(name))
        using (var row = list.Row(index, ui.GetId("row"), new Box { Gap = theme.Gap, AlignY = Align.Center }))
        {
            ActiveMark(ui, ui.State.Appearance, active);
            if (dialog.Mode is ListsDialogMode.Renaming renaming && renaming.List == name)
            {
                EditName(ui, renaming, listId);
            }
            else
            {
                NameText(ui, name, row.Selected);
            }

            var count = lists.GetPaths(name).Count;
            ui.Text(t.ListsCount(count), color: theme.TextMuted, size: theme.FontSizeSmall);
            if (ui.Button(t.ListsActivate, enabled: !active, id: "activate", variant: ButtonVariant.Ghost))
            {
                Switch(ui, ui.State.AppCommands, lists, session, view, name);
            }

            return row.Interaction.DoubleClicked;
        }
    }

    private static void ActiveMark(Ui ui, Appearance appearance, bool active)
    {
        var theme = ui.Theme;
        using (ui.Box(new Box { Width = theme.IconSize, Height = Sizing.Grow() }))
        {
            if (active)
            {
                ui.Icon(new UiIcon(appearance.Icons.ListsActive, theme.Accent));
            }
        }
    }

    private static void NameText(Ui ui, string name, bool selected)
    {
        var theme = ui.Theme;
        var color = selected ? theme.SelectionText : theme.Text;
        ui.Text(name, new Box { Width = Sizing.Grow() }, color: color, overflow: TextOverflow.Ellipsis);
    }

    private static void EditName(Ui<UiState> ui, ListsDialogMode.Renaming renaming, int listId)
    {
        var begin = renaming.TryTakeFocus();
        var result = ui.InlineEdit(NameId, ref renaming.NewName, begin, Sizing.Grow());
        if (result == InlineEditResult.Editing)
        {
            return;
        }

        var stillHere = ui.HasFocus(ui.GetId(NameId));
        FinishEditingName(ui, result);
        if (stillHere || result == InlineEditResult.Cancelled)
        {
            ui.Focus(listId, takesText: false);
        }
    }

    private static void FinishEditingName(Ui<UiState> ui, InlineEditResult result)
    {
        if (result == InlineEditResult.Committed)
        {
            FinishRenaming(ui.State);
            return;
        }
        ui.State.ListsDialog.StopAsking();
    }

    private static void HandleKeys(Ui ui, AssemblyListsDialog listsDialog, SavedAssemblyLists lists)
    {
        var dialog = listsDialog;
        if (dialog.IsAsking)
        {
            return;
        }

        var picked = dialog.FindPickedList(lists);
        if (picked is null)
        {
            return;
        }

        if (ui.Input.TakeKey(KeyboardKey.F2))
        {
            dialog.StartRenaming(picked);
        }

        if (ui.IsPressed(Shortcuts.Delete))
        {
            dialog.StartConfirmingDelete(picked);
        }
    }

    private static void ActOnRow(Ui ui, AssemblyListsDialog listsDialog, IAppCommandWriter commands, SavedAssemblyLists lists, OpenListSession session, ViewSettings view, string name, bool doubleClicked)
    {
        if (doubleClicked)
        {
            listsDialog.StartRenaming(name);
            return;
        }
        Switch(ui, commands, lists, session, view, name);
    }

    private static void Switch(Ui ui, IAppCommandWriter commands, SavedAssemblyLists lists, OpenListSession session, ViewSettings view, string name)
    {
        Commands.OpenListLater(commands, AssemblyLists.SwitchAssemblyList(lists, session, view, name));
        // The rows drawn above show it as the active list only next frame.
        ui.RequestUpdate();
    }

    private static void LeavePickedRow(Ui<UiState> ui)
    {
        var dialog = ui.State.ListsDialog;
        if (dialog.Mode is ListsDialogMode.Renaming)
        {
            FinishRenaming(ui.State);
            return;
        }
        dialog.StopAsking();
    }

    private static void RenameProblem(Ui ui, Appearance appearance, AssemblyListsDialog listsDialog, SavedAssemblyLists lists)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var dialog = listsDialog;
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

    private static void ContextMenu(Ui ui, AssemblyListsDialog listsDialog, Appearance appearance, IAppCommandWriter commands, SavedAssemblyLists lists, OpenListSession session, ViewSettings view)
    {
        using var menu = ui.ContextMenu(MenuId);
        if (!menu.IsOpen)
        {
            return;
        }

        var picked = listsDialog.FindPickedList(lists);
        if (picked is null)
        {
            return;
        }

        MenuItems(ui, appearance, listsDialog, commands, lists, session, view, picked);
    }

    private static void MenuItems(Ui ui, Appearance appearance, AssemblyListsDialog listsDialog, IAppCommandWriter commands, SavedAssemblyLists lists, OpenListSession session, ViewSettings view, string picked)
    {
        var t = appearance.Strings;
        var dialog = listsDialog;
        var active = picked == lists.Active;
        if (ui.MenuItem(t.ListsActivate, enabled: !active, id: "activate"))
        {
            Switch(ui, commands, lists, session, view, picked);
        }

        if (ui.MenuItem(t.ListsRename, Shortcuts.Rename, id: "rename"))
        {
            dialog.StartRenaming(picked);
        }

        if (ui.MenuItem(t.ListsDuplicate, id: "duplicate"))
        {
            Commands.Later(commands, (dialog, name: picked, copyName: t.ListsCopyName(picked)), static (a, s) => Duplicate(s.dialog, a, s.name, s.copyName));
        }

        ui.MenuSeparator();
        if (ui.MenuItem(t.ListsDelete, Shortcuts.Delete, id: "delete"))
        {
            dialog.StartConfirmingDelete(picked);
        }
    }

    private static void Footer(Ui ui, Appearance appearance, AssemblyListsDialog listsDialog, PresetScan presets, IAppCommandWriter commands)
    {
        var t = appearance.Strings;
        var dialog = listsDialog;
        using (ui.DialogActions())
        {
            PresetMenu(ui, appearance, presets, commands, listsDialog);
            ui.Spacer();
            if (ui.Button(t.ListsClose, id: "close"))
            {
                dialog.Close();
            }
        }
    }

    private static void PresetMenu(Ui ui, Appearance appearance, PresetScan presetScan, IAppCommandWriter commands, AssemblyListsDialog listsDialog)
    {
        var t = appearance.Strings;
        using var menu = ui.SelectButton(PresetMenuId, t.ListsAddPreset);
        if (!menu.IsOpen)
        {
            return;
        }

        var scan = presetScan;
        var presets = scan.Found;
        if (presets is null)
        {
            // The frame edge right after this draw looks for them; the menu says so until they come.
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
                Commands.Later(commands, (dialog: listsDialog, preset), static (a, s) => AddPreset(s.dialog, a, s.preset));
            }
        }
    }

    private static void AddPreset(AssemblyListsDialog dialog, AppState appState, AssemblyListPreset preset)
    {
        var name = AssemblyLists.AddAssemblyListPreset(appState, preset);
        dialog.PickToRename(appState.Lists, name);
    }

    /// <summary>Opens the dialog with the open list picked, its assemblies counted as they are now.</summary>
    internal static void Open(AssemblyListsDialog dialog, AppState appState)
    {
        AssemblyLists.RememberOpenAssemblies(appState.Lists, appState.ListSession);
        dialog.Open(appState.Lists);
    }

    private static void Create(AssemblyListsDialog dialog, AppState appState, string newName)
    {
        var name = AssemblyLists.CreateAssemblyList(appState, newName);
        dialog.PickToRename(appState.Lists, name);
    }

    private static void Duplicate(AssemblyListsDialog dialog, AppState appState, string name, string copyName)
    {
        var copy = AssemblyLists.DuplicateAssemblyList(appState, name, copyName);
        dialog.PickToRename(appState.Lists, copy);
    }

    private static void FinishRenaming(UiState screen)
    {
        var dialog = screen.ListsDialog;
        if (dialog.Mode is not ListsDialogMode.Renaming renaming)
        {
            return;
        }

        dialog.StopAsking();
        Commands.Later(screen.AppCommands, (list: renaming.List, newName: renaming.NewName), static (a, s) => a.Lists.TryRename(s.list, s.newName));
    }
}
