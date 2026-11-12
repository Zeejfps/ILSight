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
    public MetadataGridRole Role => Part.Match(
        static grid => grid.Role,
        static _ => MetadataGridRole.Directory,
        static _ => MetadataGridRole.Table,
        static _ => MetadataGridRole.Table,
        static _ => MetadataGridRole.Table);

    /// <summary>The table whose rows the grid lists; null for a part that lists no table's rows.</summary>
    public TableIndex? ListedTable => Part.Match<TableIndex?>(
        static grid => grid.Schema.Table,
        static _ => null,
        static _ => null,
        static _ => null,
        static _ => null);

    /// <summary>Whether this is a table with no rows, which the tree usually hides.</summary>
    public bool IsEmptyTable => Part is MetadataPart.RowCount count && count.Rows == 0;
}
