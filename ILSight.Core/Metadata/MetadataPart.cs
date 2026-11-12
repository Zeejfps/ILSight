using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

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
