using System.Diagnostics;
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

    internal sealed record Grid : MetadataPart
    {
        public required MetadataGridRole Role { get; init; }

        public required GridSchema Schema { get; init; }

        public required LazyGrid View { get; init; }
    }

    public sealed record DebugDirectory : MetadataPart
    {
        public required PEFile File { get; init; }

        public required LazyGrid View { get; init; }
    }

    public sealed record RowCount : MetadataPart
    {
        public required TableIndex Table { get; init; }

        public required int Rows { get; init; }
    }

    public sealed record DecodedDebugEntry(IReadOnlyList<string> Lines) : MetadataPart;

    public sealed record RawDebugEntry : MetadataPart
    {
        public required PEFile File { get; init; }

        public required DebugDirectoryEntry Entry { get; init; }
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    internal TResult Match<TResult>(Func<Grid, TResult> grid, Func<DebugDirectory, TResult> debugDirectory,
        Func<RowCount, TResult> rowCount, Func<DecodedDebugEntry, TResult> decodedDebugEntry,
        Func<RawDebugEntry, TResult> rawDebugEntry)
    {
        return this switch
        {
            Grid c => grid(c),
            DebugDirectory c => debugDirectory(c),
            RowCount c => rowCount(c),
            DecodedDebugEntry c => decodedDebugEntry(c),
            RawDebugEntry c => rawDebugEntry(c),
            _ => throw new UnreachableException($"MetadataPart.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    internal TResult Match<TState, TResult>(TState state, Func<TState, Grid, TResult> grid,
        Func<TState, DebugDirectory, TResult> debugDirectory, Func<TState, RowCount, TResult> rowCount,
        Func<TState, DecodedDebugEntry, TResult> decodedDebugEntry, Func<TState, RawDebugEntry, TResult> rawDebugEntry)
    {
        return this switch
        {
            Grid c => grid(state, c),
            DebugDirectory c => debugDirectory(state, c),
            RowCount c => rowCount(state, c),
            DecodedDebugEntry c => decodedDebugEntry(state, c),
            RawDebugEntry c => rawDebugEntry(state, c),
            _ => throw new UnreachableException($"MetadataPart.Match does not handle {GetType().Name}."),
        };
    }
}
