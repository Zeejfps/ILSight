using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

/// <summary>The raw CLI metadata of a file of <see cref="Module"/>: its own, or its debug metadata, under
/// a folder titled <see cref="Title"/>.</summary>
public sealed class MetadataFolderInfo
{
    public required LoadedModule Module { get; init; }

    public required MetadataFile File { get; init; }

    public required string Title { get; init; }
}

/// <summary>What a grid of metadata lists.</summary>
public enum MetadataGridRole
{
    Header,
    Directory,
    Table,
    Heap,
}

/// <summary>Which of a PE file's headers a grid lists.</summary>
public enum PEHeaderKind
{
    Dos,
    Coff,
    Optional,
}

/// <summary>Which part of a metadata folder a node stands for, by what it is rather than how it is titled:
/// a PE header, the data or debug directory, a table, a heap, or an entry of the debug directory.</summary>
public abstract record MetadataPartKey
{
    private MetadataPartKey()
    {
    }

    public sealed record Header(PEHeaderKind Which) : MetadataPartKey;

    public sealed record DataDirectories : MetadataPartKey;

    public sealed record DebugDirectory : MetadataPartKey;

    public sealed record Table(TableIndex Index) : MetadataPartKey;

    /// <summary>A heap by its name, such as <c>String Heap</c>.</summary>
    public sealed record Heap(string Name) : MetadataPartKey;

    /// <summary>An entry of the debug directory: where it stands there, from 0, and its type.</summary>
    public sealed record DebugEntry(int Index, DebugDirectoryEntryType Type) : MetadataPartKey;
}

/// <summary>A part of a metadata folder: a grid of a header, directory, table or heap; the debug
/// directory, a grid that also lists its entries; a table shown only by how many rows it has; or a debug
/// directory entry, decoded or as its raw bytes.</summary>
public abstract record MetadataPart
{
    private MetadataPart()
    {
    }

    internal sealed record Grid(MetadataGridRole Role, GridSchema Schema, Lazy<MetadataGrid> View) : MetadataPart;

    public sealed record DebugDirectory(PEFile File, Lazy<MetadataGrid> View) : MetadataPart;

    public sealed record RowCount(TableIndex Table, int Rows) : MetadataPart;

    public sealed record DecodedDebugEntry(IReadOnlyList<string> Lines) : MetadataPart;

    public sealed record RawDebugEntry(PEFile File, DebugDirectoryEntry Entry) : MetadataPart;
}

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

/// <summary>Where a token cell leads: a row of a table node.</summary>
public readonly record struct MetadataTarget(BrowseNode Node, int Row);
