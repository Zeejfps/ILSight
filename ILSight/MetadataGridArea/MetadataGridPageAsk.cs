using ILSight.Core.Metadata;

namespace ILSight.MetadataGridArea;

/// <summary>A page of a paged grid's rows that its view asked for while it drew, for the frame edge to read.</summary>
public readonly record struct MetadataGridPageAsk
{
    public required MetadataGrid.Paged Grid { get; init; }

    public required int Page { get; init; }
}
