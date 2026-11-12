using ILSight.DiagramArea;

namespace ILSight;

/// <summary>How a docked tab's diagram or comparison is viewed, or none for a tab showing neither.</summary>
public abstract record TabPanelView
{
    private TabPanelView()
    {
    }

    public sealed record None : TabPanelView;

    public sealed record Diagram(DiagramViewRecord View) : TabPanelView;

    public sealed record Comparison(ComparisonViewRecord View) : TabPanelView;
}
