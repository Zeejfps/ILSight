using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;

namespace ILSight.MainArea;

/// <summary>The framework's dock widget, which <see cref="Panels"/> draws its layout through: as it draws, the widget
/// writes the layout itself (a drag docking or floating a panel, a close button), and the panels follow its writes
/// there.</summary>
public interface IDockWidget
{
    /// <summary>Draws <paramref name="layout"/>'s dock space, as <see cref="UiWidgets.DockSpace{TState}"/> does.</summary>
    DockSpace Draw<TState>(Ui ui, string id, DockLayout layout, TState state, Func<TState, string, DockTab> tab, string tabListTooltip);
}
