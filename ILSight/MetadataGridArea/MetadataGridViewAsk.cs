using ILSight.Core.Metadata;

namespace ILSight.MetadataGridArea;

/// <summary>The rows a grid's view asked for while it drew: sorted by <see cref="SortColumn"/> (none when negative),
/// with <see cref="Order"/> that sort when it was made before, passing a copy of the filters as they stood. Known by
/// <see cref="Key"/>, the view's key then: only the newest one asked lands.</summary>
public sealed class MetadataGridViewAsk
{
    public required MetadataGridState State { get; init; }

    public required int Key { get; init; }

    public required int SortColumn { get; init; }

    public required bool Descending { get; init; }

    public required int[]? Order { get; init; }

    public required MetadataColumnFilter?[] Filters { get; init; }
}
