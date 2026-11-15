using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.MainArea;

namespace ILSight.App.Tests;

/// <summary>Stands in for the user at the framework's dock widget, wrapping the one the root made: what a test asks of
/// it (a drag docking or floating a panel, a close button) the widget does as the panels next draw their layout through
/// it, inside their dock-space draw, where the widget's own writes land; so it takes a frame
/// (<see cref="AppDriver.Run"/>).</summary>
internal sealed class DockWidgetDriver(IDockWidget widget) : IDockWidget
{
    private readonly List<Action<DockLayout>> _asked = [];

    public DockSpace Draw<TState>(Ui ui, string id, DockLayout layout, TState state, Func<TState, string, DockTab> tab, string tabListTooltip)
    {
        foreach (var asked in _asked)
        {
            asked(layout);
        }

        _asked.Clear();
        return widget.Draw(ui, id, layout, state, tab, tabListTooltip);
    }

    /// <summary>A drag of <paramref name="panel"/>'s tab dropped on <paramref name="target"/>'s group, by its
    /// <paramref name="edge"/> (its middle joins the group), taking <paramref name="share"/> of a split.</summary>
    public void Dock(string panel, string target, DockEdge edge, float share = 0.5f)
    {
        _asked.Add(layout => layout.Dock(panel, layout.GroupOf(target) ?? throw new InvalidOperationException($"{target} is not in the layout."), edge, share));
    }

    /// <summary>A drag of <paramref name="panel"/>'s tab dropped by the dock space's rim at <paramref name="edge"/>.</summary>
    public void DockAtRim(string panel, DockEdge edge)
    {
        _asked.Add(layout => layout.Dock(panel, layout.Root, edge));
    }

    /// <summary>A drag of <paramref name="panel"/>'s tab, Ctrl held, dropped as a window at <paramref name="rect"/>.</summary>
    public void Float(string panel, UiRect rect)
    {
        _asked.Add(layout => layout.Float(panel, rect));
    }

    /// <summary>A press of the close button on <paramref name="panel"/>'s tab.</summary>
    public void Close(string panel)
    {
        _asked.Add(layout => layout.Close(panel));
    }
}
