using ILSight.DiagramArea;

namespace ILSight;

/// <summary>How a docked tab's diagram or comparison is viewed, or none for a tab showing neither.</summary>
public union TabPanelView(TabPanelView.None, TabPanelView.Diagram, TabPanelView.Comparison)
{
    public sealed record None;

    public sealed record Diagram(DiagramViewRecord View);

    public sealed record Comparison(ComparisonViewRecord View);
}
