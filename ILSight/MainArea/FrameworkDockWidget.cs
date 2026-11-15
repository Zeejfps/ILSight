using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;

namespace ILSight.MainArea;

/// <summary>The dock widget of the framework the app is drawn with.</summary>
internal sealed class FrameworkDockWidget : IDockWidget
{
    public DockSpace Draw<TState>(Ui ui, string id, DockLayout layout, TState state, Func<TState, string, DockTab> tab, string tabListTooltip)
    {
        return ui.DockSpace(id, layout, state, tab, tabListTooltip: tabListTooltip);
    }
}
