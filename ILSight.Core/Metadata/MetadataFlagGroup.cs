namespace ILSight.Core.Metadata;

/// <summary>A part of a flags value: for a mutually exclusive range, the one member it holds;
/// otherwise every independent flag, set or not.</summary>
public sealed record MetadataFlagGroup
{
    public required string? Header { get; init; }

    public required bool IsChoice { get; init; }

    public required IReadOnlyList<MetadataFlag> Flags { get; init; }
}
