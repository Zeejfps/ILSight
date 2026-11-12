namespace ILSight.Core.Metadata;

/// <summary>Where a token cell leads: a row of a table node.</summary>
public readonly record struct MetadataTarget(BrowseNode Node, int Row);
