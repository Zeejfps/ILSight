using System.Reflection.Metadata.Ecma335;

namespace ILSight.Core.Metadata;

/// <summary>A row of a table of the metadata a grid lists, as a token cell names it.</summary>
public readonly record struct MetadataAddress(TableIndex Table, int Row);
