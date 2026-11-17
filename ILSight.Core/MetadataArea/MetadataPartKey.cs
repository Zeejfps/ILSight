using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace ILSight.Core.MetadataArea;

/// <summary>Which part of a metadata folder a node stands for, by what it is rather than how it is titled:
/// a PE header, the data or debug directory, a table, a heap, or an entry of the debug directory.</summary>
public union MetadataPartKey(MetadataPartKey.Header, MetadataPartKey.DataDirectories, MetadataPartKey.DebugDirectory,
    MetadataPartKey.Table, MetadataPartKey.Heap, MetadataPartKey.DebugEntry)
{
    public sealed record Header(PEHeaderKind Which);

    public sealed record DataDirectories;

    public sealed record DebugDirectory;

    public sealed record Table(TableIndex Index);

    /// <summary>A heap by its name, such as <c>String Heap</c>.</summary>
    public sealed record Heap(string Name);

    /// <summary>An entry of the debug directory: where it stands there, from 0, and its type.</summary>
    public sealed record DebugEntry
    {
        public required int Index { get; init; }

        public required DebugDirectoryEntryType Type { get; init; }
    }
}
