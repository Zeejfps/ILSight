namespace ILSight.Core.Metadata;

/// <summary>A part of a flags value: for a mutually exclusive range, the one member it holds;
/// otherwise every independent flag, set or not.</summary>
public sealed record MetadataFlagGroup(string? Header, bool IsChoice, IReadOnlyList<MetadataFlag> Flags);
