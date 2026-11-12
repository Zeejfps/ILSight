namespace ILSight.Core.Metadata;

/// <summary>What hovering a cell explains: text, or a flags value broken down into its groups.</summary>
public sealed record MetadataTooltip
{
    public required string? Text { get; init; }

    public required IReadOnlyList<MetadataFlagGroup> Groups { get; init; }
}
