using System.Collections.Concurrent;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

/// <summary>
/// The rows of a metadata node as ILSpy shows them in a grid: a header, a directory, a table or a
/// heap, read through its <see cref="GridSchema"/>. Cell text, links, tooltips and details are
/// worked out once and kept; read them on one thread at a time.
/// </summary>
public sealed class MetadataGrid
{
    /// <summary>How many patterns are kept, which the user types, so nothing else bounds them.</summary>
    internal const int MaxPatterns = 256;

    internal static readonly ConcurrentDictionary<string, Regex?> Patterns = new();

    internal static int CachedPatterns => Patterns.Count;

    public required LoadedModule Module { get; init; }
    public required MetadataFile File { get; init; }
    public required GridSchema Schema { get; init; }
    public required IReadOnlyList<object> Rows { get; init; }
    public required IReadOnlyList<GridColumn> GridColumns { get; init; }
    public readonly Dictionary<int, MetadataTooltip?> Tooltips = new();
    public readonly Dictionary<int, MetadataAddress?> Links = new();
    public readonly Dictionary<TableIndex, bool> Listed = new();
    public readonly Dictionary<int, MetadataRowDetails?> DetailCells = new();
    internal string?[]? TextCells;
    public (string FirstLine, int MoreLines)?[]? LineCells;

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
}
