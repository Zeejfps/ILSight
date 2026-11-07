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
    public MetadataGridState? Grid;

    public PictureView Picture { get; } = new();

    public DiagramViewState Diagram { get; } = new();

    public ComparisonRows Comparison { get; } = new();

    /// <summary>The image the tab's picture is drawn as.</summary>
    public required string ImageId { get; init; }
}
