namespace ILSight.Core.Metadata;

/// <summary>A page of rows of a <see cref="MetadataGrid.Paged"/>, as read on the I/O side: each cell's value (or its
/// <see cref="MetadataCellFailure"/>), tooltip and link by its index in the page (row in the page × columns + column),
/// and each row's details and definition by the row's place in the page; null where there is nothing.</summary>
public sealed class MetadataGridPage
{
    public required IReadOnlyList<object?> Values { get; init; }

    public required IReadOnlyList<MetadataTooltip?> Tooltips { get; init; }

    public required IReadOnlyList<MetadataAddress?> Links { get; init; }

    public required IReadOnlyList<MetadataRowDetails?> Details { get; init; }

    public required IReadOnlyList<CodeReference.Navigable?> Definitions { get; init; }
}
