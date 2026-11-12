using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

/// <summary>A part of the metadata of a file of <see cref="Module"/>, titled <see cref="Title"/>: a grid
/// of a header, directory, table or heap, the debug directory, a table shown only by its row count, or
/// an entry of the debug directory.</summary>
public sealed class MetadataTableInfo
{
    public required LoadedModule Module { get; init; }

    public required MetadataFile File { get; init; }

    public required string Title { get; init; }

    /// <summary>Which part this is, whatever its title says.</summary>
    public required MetadataPartKey Key { get; init; }

    public required MetadataPart Part { get; init; }

    /// <summary>What the part lists as a grid would: the debug directory a directory, and a table shown
    /// by its row count or a debug entry a table.</summary>
    public MetadataGridRole Role => Part switch
    {
        MetadataPart.Grid grid => grid.Role,
        MetadataPart.DebugDirectory => MetadataGridRole.Directory,
        MetadataPart.RowCount or MetadataPart.DecodedDebugEntry or MetadataPart.RawDebugEntry => MetadataGridRole.Table,
        _ => throw new UnreachableException(),
    };

    /// <summary>The table whose rows the grid lists; null for a part that lists no table's rows.</summary>
    public TableIndex? ListedTable => Part switch
    {
        MetadataPart.Grid grid => grid.Schema.Table,
        MetadataPart.DebugDirectory or MetadataPart.RowCount or MetadataPart.DecodedDebugEntry or MetadataPart.RawDebugEntry => null,
        _ => throw new UnreachableException(),
    };

    /// <summary>Whether this is a table with no rows, which the tree usually hides.</summary>
    public bool IsEmptyTable => Part is MetadataPart.RowCount count && count.Rows == 0;
}
