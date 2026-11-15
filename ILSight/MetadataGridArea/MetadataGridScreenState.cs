using ILSight.MetadataArea;

namespace ILSight.MetadataGridArea;

/// <summary>The metadata grids' own state on the screen, which every grid draws with: the flags filters kept for each
/// kind of rows, and the sorts, filters and pages the grids asked for while the screen drew, for the frame edge to
/// start off the screen's thread.</summary>
public sealed class MetadataGridScreenState
{
    public required MetadataFilters Filters { get; init; }

    public MetadataGridViews Views { get; } = new();

    public MetadataGridPages Pages { get; } = new();
}
