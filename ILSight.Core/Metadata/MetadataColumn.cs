namespace ILSight.Core.Metadata;

/// <summary>One column of a <see cref="MetadataGrid"/>.</summary>
public sealed record MetadataColumn
{
    /// <summary>ILSpy's header for it.</summary>
    public required string Name { get; init; }

    /// <summary>Whether its cells are metadata tokens that <see cref="MetadataGrids.FindLink"/> can follow.</summary>
    public required bool IsToken { get; init; }

    /// <summary>Whether it holds numbers, which read best aligned to the right.</summary>
    public required bool IsNumeric { get; init; }

    /// <summary>For a column of flags, how its bits divide into groups, which a <see cref="FilterState"/> filters
    /// on.</summary>
    public FlagsSchema? Flags { get; init; }
}
