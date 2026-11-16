namespace ILSight.Core.MetadataArea;

/// <summary>One named flag in a <see cref="MetadataFlagGroup"/>, and whether it is set.</summary>
public readonly record struct MetadataFlag
{
    public required string Name { get; init; }

    public required bool IsSet { get; init; }
}
