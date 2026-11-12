using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

/// <summary>
/// The rows of a metadata node as ILSpy shows them in a grid: a header, a directory, a table or a heap, read through
/// its <see cref="GridSchema"/>, one of two ways by what reading a cell can reach. <see cref="InMemory"/>: its file was
/// read whole into memory, so a cell is read where it is drawn, its failure shown in its place. <see cref="ReadWhole"/>:
/// its file is one the library maps (a WebCIL module's), each read reaching the disk, so every cell, tooltip, link,
/// detail and definition was read when the grid was made, on the I/O side, and the draw reads values. Cell text,
/// links, tooltips and details are worked out once per cell viewed and kept; read them on one thread at a time (the
/// screen's). Sorting and filtering (<see cref="MetadataGrids.ArrangeRows"/>) read the cells' values alone, never
/// what is kept, so they may run off it.
/// </summary>
public abstract class MetadataGrid
{
    /// <summary>How many patterns are kept, which the user types, so nothing else bounds them.</summary>
    internal const int MaxPatterns = 256;

    internal static readonly ConcurrentDictionary<string, Regex?> Patterns = new();

    internal static int CachedPatterns => Patterns.Count;

    private MetadataGrid()
    {
    }

    public required LoadedModule Module { get; init; }
    public required GridSchema Schema { get; init; }
    public required IReadOnlyList<object> Rows { get; init; }
    public required IReadOnlyList<GridColumn> GridColumns { get; init; }
    public readonly Dictionary<int, MetadataTooltip?> Tooltips = new();
    public readonly Dictionary<int, MetadataAddress?> Links = new();
    public readonly Dictionary<TableIndex, bool> Listed = new();
    public readonly Dictionary<int, MetadataRowDetails?> DetailCells = new();
    internal readonly Dictionary<int, CodeReference.Navigable?> Definitions = new();

    /// <summary>The text of each cell viewed, by its index (row × columns + column).</summary>
    internal readonly Dictionary<int, string> TextCells = new();

    /// <summary>The first line of each cell viewed and how many lines follow it, by its index.</summary>
    internal readonly Dictionary<int, (string FirstLine, int MoreLines)> LineCells = new();

    /// <summary>What names the kind of rows, the same for a table in any assembly: where a
    /// column's flags filter is kept.</summary>
    public string Key => Schema.Key;

    /// <summary>Whether the selected row shows its <see cref="MetadataGrids.FindDetails"/> under it, as a debug
    /// information's decoded value does.</summary>
    public bool HasDetails => Schema.Details is not null;

    /// <summary>What the grid lists, as its node is labelled.</summary>
    public required string Title { get; init; }

    public required IReadOnlyList<MetadataColumn> Columns { get; init; }

    public int RowCount => Rows.Count;

    /// <summary>A grid whose file is in memory: each cell is read through it where it is needed, a draw
    /// included.</summary>
    public sealed class InMemory : MetadataGrid
    {
        public required MetadataFile File { get; init; }
    }

    /// <summary>A grid whose file the library maps: every cell's value was read when it was made, each a value or
    /// its <see cref="MetadataCellFailure"/>, by its index (row × columns + column), and its tooltips, links,
    /// details and definitions are all kept.</summary>
    public sealed class ReadWhole : MetadataGrid
    {
        public required IReadOnlyList<object?> Values { get; init; }
    }

    public TResult Match<TResult>(Func<InMemory, TResult> inMemory, Func<ReadWhole, TResult> readWhole)
    {
        return this switch
        {
            InMemory m => inMemory(m),
            ReadWhole w => readWhole(w),
            _ => throw new UnreachableException($"MetadataGrid.Match does not handle {GetType().Name}."),
        };
    }
}
