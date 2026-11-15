using ZGF.Gui.Immediate;
using ILSight.DocumentViewArea;

namespace ILSight.MainArea;

/// <summary>What the window shows where the dock space goes while every panel is closed.</summary>
internal static class EmptyLayoutView
{
    /// <summary>Draws a hint pointing to the View menu while <paramref name="panels"/> has no panel open; returns whether
    /// it did, the dock space then not drawn.</summary>
    public static bool Render(Ui ui, Appearance appearance, Panels panels)
    {
        if (panels.HasPanels)
        {
            return false;
        }

        DocumentsView.Hint(ui, appearance.Strings.ViewEmptyHint);
        return true;
    }
}
