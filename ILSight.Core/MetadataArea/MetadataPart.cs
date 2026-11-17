using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.MetadataArea;

/// <summary>A part of a metadata folder: a grid of a header, directory, table or heap; the debug
/// directory, a grid that also lists its entries; a table shown only by how many rows it has; or a debug
/// directory entry, decoded or as its raw bytes.</summary>
public union MetadataPart(MetadataPart.Grid, MetadataPart.DebugDirectory, MetadataPart.RowCount, MetadataPart.DecodedDebugEntry,
    MetadataPart.RawDebugEntry)
{
    internal sealed record Grid
    {
        public required MetadataGridRole Role { get; init; }

        public required GridSchema Schema { get; init; }

        public required LazyGrid View { get; init; }
    }

    public sealed record DebugDirectory
    {
        public required PEFile File { get; init; }

        public required LazyGrid View { get; init; }
    }

    public sealed record RowCount
    {
        public required TableIndex Table { get; init; }

        public required int Rows { get; init; }
    }

    public sealed record DecodedDebugEntry(IReadOnlyList<string> Lines);

    public sealed record RawDebugEntry
    {
        public required PEFile File { get; init; }

        public required DebugDirectoryEntry Entry { get; init; }
    }
}
