using ILSight.Core.Metadata;

namespace ILSight.MetadataArea;

/// <summary>The flags filters of the metadata grids: one per kind of row and column, shared by every
/// grid of that kind in any assembly, as ILSpy's are, and kept between runs.</summary>
public sealed class MetadataFilters
{
    /// <summary>What filters something, as read and as last listed, by the kind of row and column.</summary>
    public required Dictionary<string, SavedFilterState> Saved { get; init; }

    /// <summary>The filters used so far, by the kind of row and column.</summary>
    public readonly Dictionary<string, FilterState> States = new();

    /// <summary>Changes whenever a filter does.</summary>
    public int Revision => States.Values.Sum(static s => s.Revision);
}
