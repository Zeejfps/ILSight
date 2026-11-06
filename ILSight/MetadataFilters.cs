using ILSight.Core.Metadata;

namespace ILSight;

/// <summary>The flags filters of the metadata grids: one per kind of row and column, shared by every
/// grid of that kind in any assembly, as ILSpy's are, and kept between runs.</summary>
public sealed class MetadataFilters
{
    /// <summary>What filters something, as read and as last listed, by the kind of row and column.</summary>
    public readonly Dictionary<string, SavedFilterState> Saved;

    /// <summary>The filters used so far, by the kind of row and column.</summary>
    public readonly Dictionary<string, FilterState> States = new();

    public MetadataFilters(Dictionary<string, SavedFilterState>? saved)
    {
        Saved = saved != null ? new Dictionary<string, SavedFilterState>(saved) : new Dictionary<string, SavedFilterState>();
    }

    /// <summary>Changes whenever a filter does.</summary>
    public int Revision => States.Values.Sum(static s => s.Revision);
}
