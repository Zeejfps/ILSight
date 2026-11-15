using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;

namespace ILSight.MainArea;

/// <summary>Asks whether to put every panel back where it started before the layout is reset.</summary>
internal static class LayoutResetDialogView
{
    public static void Render(Ui<UiState> ui)
    {
        var t = ui.State.Appearance.Strings;
        var open = ui.State.LayoutReset.IsAsked;
        using (var dialog = ui.Dialog("layout-reset", t.ViewResetLayout, ref open, closeButton: false))
        {
            if (dialog.IsOpen)
            {
                ui.Text(t.ViewResetLayoutQuestion, wrap: true);
                using (ui.DialogActions())
                {
                    if (ui.Button(t.ViewResetLayoutCancel, id: "reset-cancel", variant: ButtonVariant.Ghost))
                    {
                        ScreenLayout.FinishLayoutReset(ui.State.LayoutReset, ui.State.Panels, ui.State.AppCommands, false);
                    }

                    if (ui.Button(t.ViewResetLayoutConfirm, id: "reset-confirm", variant: ButtonVariant.Primary))
                    {
                        ScreenLayout.FinishLayoutReset(ui.State.LayoutReset, ui.State.Panels, ui.State.AppCommands, true);
                    }
                }
            }
        }
        if (!open && ui.State.LayoutReset.IsAsked)
        {
            ScreenLayout.FinishLayoutReset(ui.State.LayoutReset, ui.State.Panels, ui.State.AppCommands, false);
        }
    }
}
