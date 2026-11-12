using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using static ILSight.AssemblyLists;

namespace ILSight;

/// <summary>Asks, over the assembly lists dialog, whether to delete the list picked there, saying
/// which list opens in its place when it is the active one.</summary>
internal static class AssemblyListDeleteDialogView
{
    public static void Render(Ui<UiState> ui, SavedAssemblyLists lists)
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
                        AssemblyLists.StopAsking(dialog);
                    }

                    if (ui.Button(t.ListsDeleteConfirm, id: "delete-confirm", variant: ButtonVariant.Primary))
                    {
                        Commands.Start(ui.State, nameof(DeleteAssemblyListAsync), (dialog, name), static (a, s, ct) => DeleteAsync(s.dialog, a, s.name, ct));
                    }
                }
            }
        }
        if (!open)
        {
            AssemblyLists.StopAsking(dialog);
        }
    }

    /// <summary>Removes the list named <paramref name="name"/> and picks the open list, which takes its
    /// place when it was open.</summary>
    private static Task<TabLoads> DeleteAsync(AssemblyListsDialog dialog, AppState appState, string name, CancellationToken ct)
    {
        var deleting = DeleteAssemblyListAsync(appState, name, ct);
        AssemblyLists.PickList(dialog, appState.Lists, appState.Lists.Active);
        return deleting;
    }
}
