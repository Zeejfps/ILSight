using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace ILSight.Core.Metadata;

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

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Header, TResult> header, Func<DataDirectories, TResult> dataDirectories,
        Func<DebugDirectory, TResult> debugDirectory, Func<Table, TResult> table, Func<Heap, TResult> heap,
        Func<DebugEntry, TResult> debugEntry)
    {
        return this switch
        {
            Header c => header(c),
            DataDirectories c => dataDirectories(c),
            DebugDirectory c => debugDirectory(c),
            Table c => table(c),
            Heap c => heap(c),
            DebugEntry c => debugEntry(c),
            _ => throw new UnreachableException($"MetadataPartKey.Match does not handle {GetType().Name}."),
        };
    }
}
