namespace ILSight.Core.Metadata;

/// <summary>One column of a <see cref="MetadataGrid"/>.</summary>
/// <param name="Name">ILSpy's header for it.</param>
/// <param name="IsToken">Whether its cells are metadata tokens that <see cref="MetadataGrid.Link"/>
/// can follow.</param>
/// <param name="IsNumeric">Whether it holds numbers, which read best aligned to the right.</param>
/// <param name="Flags">For a column of flags, how its bits divide into groups, which a
/// <see cref="FilterState"/> filters on.</param>
public sealed record MetadataColumn(string Name, bool IsToken, bool IsNumeric, FlagsSchema? Flags = null);
