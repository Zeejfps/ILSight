using ILSight.Core.Metadata;
using ILSight.CodeViewArea;
using ILSight.MetadataGridArea;
using ILSight.DiagramArea;

namespace ILSight;

/// <summary>How one open tab is shown: the panel it is docked as, the path its omnibar shows, its code view
/// and the note it shows while there is no code, its grid view, its picture, the image the picture is drawn as,
/// its diagram, and the rows of its comparison. Made when the tab opens, and dropped, with its image, when it closes.</summary>
public sealed class TabView
{
    /// <summary>The name of the tab's panel in the layout.</summary>
    public required string Panel { get; init; }

    /// <summary>The path to the node the tab shows, for its omnibar.</summary>
    public Breadcrumb Breadcrumb { get; } = new();

    public CodeViewState Code { get; } = new();

    public TabPlaceholder Placeholder { get; } = new();

    public ShownReport Report { get; } = new();

    /// <summary>The view of the grid last shown.</summary>
    public MetadataGridState? Grid { get; private set; }

    public PictureView Picture { get; } = new();

    public DiagramViewState Diagram { get; } = new();

    public ComparisonRows Comparison { get; } = new();

    /// <summary>The image the tab's picture is drawn as.</summary>
    public required string ImageId { get; init; }

    /// <summary>What of the tab this view last took, drawn or returned to a place in, which where it stands belongs
    /// to; null before it took any (<see cref="RecordShown"/>).</summary>
    public TabVersion? Shown { get; private set; }

    /// <summary>Whether what it last took was a grid, whose place is <see cref="Grid"/>'s, rather than code.</summary>
    public bool ShownGrid { get; private set; }

    /// <summary>Records that the view takes what <paramref name="tab"/> shows now, as it draws it or is returned to a
    /// place in it: where it stands from now on belongs to that. Allocates nothing while the tab shows what the view
    /// took last.</summary>
    public void RecordShown(DocumentTab tab)
    {
        ShownGrid = tab.Grid is not null;
        var shown = Shown;
        if (shown is not null && shown.IsShownBy(tab))
        {
            return;
        }

        Shown = TabVersion.Of(tab);
    }

    /// <summary>Where the view stands in what it last took (<see cref="RecordShown"/>), its grid's or its code's,
    /// whatever its tab shows since.</summary>
    public ViewPosition GetShownPosition()
    {
        var grid = Grid;
        if (ShownGrid && grid is not null)
        {
            return ViewPosition.OfGrid(grid.Position);
        }

        return ViewPosition.OfCode(Code.Position);
    }

    /// <summary>The view of <paramref name="grid"/>: the one kept for it, or a new one when the tab
    /// showed another grid, or none, before.</summary>
    public MetadataGridState GetGridState(MetadataGrid grid)
    {
        var shown = Grid;
        if (shown is not null && ReferenceEquals(shown.Grid, grid))
        {
            return shown;
        }

        shown = MetadataGridWidget.CreateState(grid);
        Grid = shown;
        return shown;
    }

    /// <summary>Where the view of what <paramref name="tab"/> shows stands: its grid's or its code's.</summary>
    public ViewPosition GetPosition(DocumentTab tab)
    {
        var grid = tab.Grid;
        if (grid is not null)
        {
            return ViewPosition.OfGrid(GetGridState(grid).Position);
        }

        return ViewPosition.OfCode(Code.Position);
    }
}
