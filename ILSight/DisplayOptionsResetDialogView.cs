using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;

namespace ILSight;

/// <summary>Asks whether to put every display option back to its default before they are reset.</summary>
internal static class DisplayOptionsResetDialogView
{
    public static void Render(Ui<UiState> ui)
    {
        var t = ui.State.Appearance.Strings;
        var open = ui.State.Display.Reset.IsAsked;
        using (var dialog = ui.Dialog("display-reset", t.DisplayOptionsReset, ref open, closeButton: false))
        {
            if (dialog.IsOpen)
            {
                ui.Text(t.DisplayOptionsResetQuestion, wrap: true);
                using (ui.DialogActions())
                {
                    if (ui.Button(t.DisplayOptionsResetCancel, id: "display-reset-cancel", variant: ButtonVariant.Ghost))
                    {
                        ui.State.Display.FinishReset(false);
                    }

                    if (ui.Button(t.DisplayOptionsResetConfirm, id: "display-reset-confirm", variant: ButtonVariant.Primary))
                    {
                        ui.State.Display.FinishReset(true);
                    }
                }
            }
        }
        if (!open && ui.State.Display.Reset.IsAsked)
        {
            ui.State.Display.FinishReset(false);
        }
    }
}
