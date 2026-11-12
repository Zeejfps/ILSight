namespace ILSight.Core.Metadata;

/// <summary>Where a token cell leads: a row of a table node.</summary>
public readonly record struct MetadataTarget
{
    public required BrowseNode Node { get; init; }

    public required int Row { get; init; }
}
