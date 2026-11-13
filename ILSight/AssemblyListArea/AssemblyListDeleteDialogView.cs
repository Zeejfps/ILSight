using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.CommandArea;

namespace ILSight.AssemblyListArea;

/// <summary>Asks, over the assembly lists dialog, whether to delete the list picked there, saying
/// which list opens in its place when it is the active one.</summary>
internal static class AssemblyListDeleteDialogView
{
    /// <summary>The question over the lists dialog. Deleting the open list switches to the one taking its place in this
    /// frame, over <paramref name="session"/>, as <see cref="AssemblyLists.DeleteAssemblyList"/> does; only opening its
    /// files is queued.</summary>
    public static void Render(Ui<UiState> ui, SavedAssemblyLists lists, OpenListSession session, ViewSettings view)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var dialog = ui.State.ListsDialog;
        if (dialog.Mode is not ListsDialogMode.ConfirmingDelete deleting)
        {
            return;
        }

        var name = deleting.List;
        var open = true;
        using (var scope = ui.Dialog("assembly-list-delete", t.ListsDeleteTitle, ref open, closeButton: false))
        {
            if (scope.IsOpen)
            {
                ui.Text(t.ListsDeleteQuestion(name), wrap: true);
                if (name == lists.Active)
                {
                    ui.Text(t.ListsDeleteReplacedBy(lists.GetReplacement(name)), color: theme.TextMuted, wrap: true);
                }

                using (ui.DialogActions())
                {
                    if (ui.Button(t.ListsCancel, id: "delete-cancel", variant: ButtonVariant.Ghost))
                    {
                        dialog.StopAsking();
                    }

                    if (ui.Button(t.ListsDeleteConfirm, id: "delete-confirm", variant: ButtonVariant.Primary))
                    {
                        Commands.OpenListLater(ui.State, Delete(dialog, lists, session, view, name));
                        ui.RequestUpdate();
                    }
                }
            }
        }
        if (!open)
        {
            dialog.StopAsking();
        }
    }

    private static ListOpening? Delete(AssemblyListsDialog dialog, SavedAssemblyLists lists, OpenListSession session, ViewSettings view, string name)
    {
        var opening = AssemblyLists.DeleteAssemblyList(lists, session, view, name);
        dialog.PickList(lists, lists.Active);
        return opening;
    }
}
