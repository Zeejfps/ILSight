using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;

namespace ILSight;

/// <summary>Asks whether to put every display option back to its default before they are reset.</summary>
internal static class DisplayOptionsResetDialogView
{
    public static void Render(Ui ui, Appearance appearance, ScreenDisplay screenDisplay)
    {
        var t = appearance.Strings;
        var open = screenDisplay.Reset.IsAsked;
        using (var dialog = ui.Dialog("display-reset", t.DisplayOptionsReset, ref open, closeButton: false))
        {
            if (dialog.IsOpen)
            {
                ui.Text(t.DisplayOptionsResetQuestion, wrap: true);
                using (ui.DialogActions())
                {
                    if (ui.Button(t.DisplayOptionsResetCancel, id: "display-reset-cancel", variant: ButtonVariant.Ghost))
                    {
                        screenDisplay.FinishReset(false);
                    }

                    if (ui.Button(t.DisplayOptionsResetConfirm, id: "display-reset-confirm", variant: ButtonVariant.Primary))
                    {
                        screenDisplay.FinishReset(true);
                    }
                }
            }
        }
        if (!open && screenDisplay.Reset.IsAsked)
        {
            screenDisplay.FinishReset(false);
        }
    }
}
