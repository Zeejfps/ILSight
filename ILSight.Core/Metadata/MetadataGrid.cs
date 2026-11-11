using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

/// <summary>One column of a <see cref="MetadataGrid"/>.</summary>
/// <param name="Name">ILSpy's header for it.</param>
/// <param name="IsToken">Whether its cells are metadata tokens that <see cref="MetadataGrid.Link"/>
/// can follow.</param>
/// <param name="IsNumeric">Whether it holds numbers, which read best aligned to the right.</param>
/// <param name="Flags">For a column of flags, how its bits divide into groups, which a
/// <see cref="FilterState"/> filters on.</param>
public sealed record MetadataColumn(string Name, bool IsToken, bool IsNumeric, FlagsSchema? Flags = null);

/// <summary>What one column of a <see cref="MetadataGrid"/> is filtered by, as ILSpy's header filters do:
/// text for most columns, and for a column of flags which of them a row is to have.</summary>
public abstract class MetadataColumnFilter
{
    private MetadataColumnFilter()
    {
    }

    /// <summary>A <c>/regex/</c>, a comparison such as <c>&gt;= 0x100</c> for a number, or else a part
    /// of the cell's text, ignoring case; nothing filters nothing.</summary>
    public sealed class ByText : MetadataColumnFilter
    {
        public string Text { get; set; } = "";
    }

    /// <summary>The flags picked for a column of flags.</summary>
    public sealed class ByFlags : MetadataColumnFilter
    {
        public required FilterState Flags { get; init; }
    }

    public bool IsActive => this switch
    {
        ByText text => text.Text.Length > 0,
        ByFlags flags => !flags.Flags.IsEmpty,
        _ => throw new UnreachableException(),
    };
}

/// <summary>What is shown under the selected row of a <see cref="MetadataGrid"/>.</summary>
public abstract record MetadataRowDetails;

/// <summary>A decoded text, with the file extension of the language it is in, if known.</summary>
public sealed record MetadataTextDetails(string Text, string? Extension) : MetadataRowDetails;

/// <summary>A decoded list, as a table.</summary>
public sealed record MetadataTableDetails(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) : MetadataRowDetails;

/// <summary>One named flag in a <see cref="MetadataFlagGroup"/>, and whether it is set.</summary>
public readonly record struct MetadataFlag(string Name, bool IsSet);

/// <summary>A part of a flags value: for a mutually exclusive range, the one member it holds;
/// otherwise every independent flag, set or not.</summary>
public sealed record MetadataFlagGroup(string? Header, bool IsChoice, IReadOnlyList<MetadataFlag> Flags);

/// <summary>What hovering a cell explains: text, or a flags value broken down into its groups.</summary>
public sealed record MetadataTooltip(string? Text, IReadOnlyList<MetadataFlagGroup> Groups);

/// <summary>A row of a table of the metadata a grid lists, as a token cell names it.</summary>
public readonly record struct MetadataAddress(TableIndex Table, int Row);

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

    /// <summary>Whether the selected row shows its <see cref="MetadataGrid.GetDetails"/> under it, as a debug
    /// information's decoded value does.</summary>
    public bool HasDetails => Schema.Details is not null;

    /// <summary>What the grid lists, as its node is labelled.</summary>
    public required string Title { get; init; }

    public required IReadOnlyList<MetadataColumn> Columns { get; init; }

    public int RowCount => Rows.Count;
}

/// <summary>Reading the cells of a <see cref="MetadataGrid"/>: their text, links, tooltips and details, each worked out once and kept.</summary>
public static class MetadataGrids
{
    /// <summary>The grid titled <paramref name="title"/> of what <paramref name="schema"/> reads from
    /// <paramref name="file"/>, which is <paramref name="module"/>'s own metadata or its debug metadata.</summary>
    public static MetadataGrid CreateGrid(string title, LoadedModule module, MetadataFile file, GridSchema schema)
    {
        var columns = schema.Columns;
        return new MetadataGrid
        {
            Title = title,
            Module = module,
            File = file,
            Schema = schema,
            Rows = schema.ReadRows(),
            GridColumns = columns,
            Columns = columns.Select(static c => c.Column).ToArray(),
        };
    }

    /// <summary>The text of a cell: numbers in the column's format, often hex.</summary>
    public static string GetText(MetadataGrid grid, int row, int column)
    {
        var text = grid.TextCells ??= new string?[grid.Rows.Count * grid.GridColumns.Count];
        var index = row * grid.GridColumns.Count + column;
        return text[index] ??= FormatValue(GetValue(grid, row, column), grid.GridColumns[column].Format);
    }

    /// <summary>The rows of <paramref name="order"/>, or of the table in its own order, that pass
    /// every one of <paramref name="filters"/>, one per column or null.</summary>
    public static int[] FilterRows(MetadataGrid grid, int[]? order, IReadOnlyList<MetadataColumnFilter?> filters)
    {
        var active = new List<(int Column, MetadataColumnFilter Filter)>();
        var columns = Math.Min(filters.Count, grid.GridColumns.Count);
        for (var c = 0; c < columns; c++)
        {
            var filter = filters[c];
            if (filter is not null && filter.IsActive)
            {
                active.Add((c, filter));
            }
        }
        var rows = order ?? Enumerable.Range(0, grid.Rows.Count).ToArray();
        if (active.Count == 0)
        {
            return rows;
        }

        return rows.Where(row => active.TrueForAll(a => MatchesCell(grid, row, a.Column, a.Filter))).ToArray();
    }

    /// <summary>What is shown under <paramref name="row"/> while it is selected; null when it has
    /// nothing.</summary>
    public static MetadataRowDetails? GetDetails(MetadataGrid grid, int row)
    {
        var details = grid.Schema.Details;
        if (details is null)
        {
            return null;
        }

        if (grid.DetailCells.TryGetValue(row, out var known))
        {
            return known;
        }

        MetadataRowDetails? result;
        try
        {
            result = details(grid.Rows[row]);
        }
        catch (Exception e) when (IsUnreadable(e))
        {
            result = null;
        }
        grid.DetailCells[row] = result;
        return result;
    }

    /// <summary>A cell's text up to its first line break, and how many lines follow it — none for
    /// text on one line.</summary>
    public static (string FirstLine, int MoreLines) GetLines(MetadataGrid grid, int row, int column)
    {
        var lines = grid.LineCells ??= new (string, int)?[grid.Rows.Count * grid.GridColumns.Count];
        var index = row * grid.GridColumns.Count + column;
        var known = lines[index];
        if (known is not null)
        {
            return known.Value;
        }

        var text = GetText(grid, row, column).TrimEnd('\r', '\n');
        var end = text.IndexOf('\n');
        var result = end < 0 ? (text, 0) : (text[..end].TrimEnd('\r'), text.AsSpan(end).Count('\n'));
        lines[index] = result;
        return result;
    }

    /// <summary>The order that sorts the rows by <paramref name="column"/>, as row indices; ties
    /// keep the table's order.</summary>
    public static int[] OrderRows(MetadataGrid grid, int column, bool descending)
    {
        var keys = new object?[grid.Rows.Count];
        for (var row = 0; row < keys.Length; row++)
        {
            keys[row] = GetValue(grid, row, column);
        }

        var order = Enumerable.Range(0, keys.Length).ToArray();
        var sign = descending ? -1 : 1;
        Array.Sort(order, (a, b) =>
        {
            var compared = Compare(keys[a], keys[b]);
            return compared != 0 ? sign * compared : a.CompareTo(b);
        });
        return order;
    }

    /// <summary>What hovering a cell explains, or null when its column has nothing to say.</summary>
    public static MetadataTooltip? GetTooltip(MetadataGrid grid, int row, int column)
    {
        var info = grid.GridColumns[column];
        if (info.Column.Flags is null && info.Tooltip is null)
        {
            return null;
        }

        var index = row * grid.GridColumns.Count + column;
        if (grid.Tooltips.TryGetValue(index, out var known))
        {
            return known;
        }

        object? value;
        try
        {
            value = info.Column.Flags is not null
                ? ExplainFlags(info.Column.Flags, GetValue(grid, row, column))
                : info.Tooltip?.Invoke(grid.Rows[row], row + 1);
        }
        catch (Exception e) when (IsUnreadable(e))
        {
            value = null;
        }
        var result = value switch
        {
            MetadataTooltip given => given,
            string text when !string.IsNullOrWhiteSpace(text) => new MetadataTooltip(text, []),
            _ => null,
        };
        grid.Tooltips[index] = result;
        return result;
    }

    /// <summary>The row a token cell points at, in the table of this metadata it names; null for a
    /// cell that is not a token, a nil token or a table this metadata lists no rows of. A row past
    /// the table's end, as the end of an empty member list is, leads to the last row.</summary>
    public static MetadataAddress? GetLink(MetadataGrid grid, int row, int column)
    {
        if (!grid.GridColumns[column].Column.IsToken)
        {
            return null;
        }

        var index = row * grid.GridColumns.Count + column;
        if (grid.Links.TryGetValue(index, out var known))
        {
            return known;
        }

        var result = FindLink(grid, row, column);
        grid.Links[index] = result;
        return result;
    }

    private static MetadataAddress? FindLink(MetadataGrid grid, int row, int column)
    {
        if (GetValue(grid, row, column) is not int token || (token & 0xFFFFFF) == 0)
        {
            return null;
        }

        var table = (TableIndex)(token >>> 24);
        if (!IsTableListed(grid, table))
        {
            return null;
        }

        var count = grid.File.Metadata.GetTableRowCount(table);
        return new MetadataAddress(table, Math.Min(token & 0xFFFFFF, count) - 1);
    }

    /// <summary>Whether this metadata's tables list <paramref name="table"/> as a grid of rows, as they
    /// do a table they can read that has rows.</summary>
    private static bool IsTableListed(MetadataGrid grid, TableIndex table)
    {
        if (grid.Listed.TryGetValue(table, out var known))
        {
            return known;
        }

        var listed = Enum.IsDefined(table) && grid.File.Metadata.GetTableRowCount(table) > 0 && MetadataTables.Create(table, grid.File) is not null;
        grid.Listed[table] = listed;
        return listed;
    }

    /// <summary>The row of <paramref name="token"/>'s table it is, or -1 when this does not list
    /// that table.</summary>
    public static int GetRow(MetadataGrid grid, int token)
    {
        return grid.Schema.Table is not null && (int)grid.Schema.Table == token >>> 24 ? (token & 0xFFFFFF) - 1 : -1;
    }

    /// <summary>
    /// What the row stands for in the decompiled code, as the double-click on it goes to: the type,
    /// member or assembly it is — a reference resolved to its definition. Null for a row that is
    /// none, or a row of debug metadata.
    /// </summary>
    public static CodeReference.Navigable? FindDefinition(MetadataGrid grid, int row)
    {
        var file = grid.File;
        if (!ReferenceEquals(file, grid.Module.File) || grid.Schema.Table is null)
        {
            return null;
        }

        var token = (int)grid.Schema.Table << 24 | row + 1;
        var handle = MetadataTokens.EntityHandle(token);
        var name = handle.Kind == HandleKind.AssemblyDefinition ? grid.Module.Name
            : grid.Schema.NameColumn >= 0 ? GetText(grid, row, grid.Schema.NameColumn)
            : token.ToString("X8");
        return CodeReferences.Find(file.Metadata, handle, name, AssemblyPath.Of(file.FileName));
    }

    private static bool MatchesCell(MetadataGrid grid, int row, int column, MetadataColumnFilter filter)
    {
        var value = GetValue(grid, row, column);
        if (value is null)
        {
            return false;
        }

        return filter switch
        {
            MetadataColumnFilter.ByFlags flags => flags.Flags.IsEmpty || FilterStatePresenter.MatchesFilter(flags.Flags, GetBits(value)),
            MetadataColumnFilter.ByText text => MatchesText(grid, row, column, value, text.Text),
            _ => throw new UnreachableException(),
        };
    }

    private static bool MatchesText(MetadataGrid grid, int row, int column, object value, string text)
    {
        if (text.Length == 0)
        {
            return true;
        }

        var pattern = FindPattern(text);
        if (pattern is not null)
        {
            return pattern.IsMatch(value.ToString() ?? "");
        }

        if (grid.GridColumns[column].Column.IsNumeric)
        {
            var compared = Compare(value, text);
            if (compared is not null)
            {
                return compared.Value;
            }
        }
        return GetText(grid, row, column).Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What the flags set in <paramref name="cell"/> mean; null for an empty cell.</summary>
    private static MetadataTooltip? ExplainFlags(FlagsSchema flags, object? cell)
    {
        if (cell is null)
        {
            return null;
        }

        return FilterStatePresenter.Explain(flags, GetBits(cell));
    }

    internal static Regex? FindPattern(string text)
    {
        if (text.Length < 2 || text[0] != '/' || text[^1] != '/')
        {
            return null;
        }

        if (MetadataGrid.Patterns.TryGetValue(text, out var kept))
        {
            return kept;
        }

        if (MetadataGrid.Patterns.Count >= MetadataGrid.MaxPatterns)
        {
            MetadataGrid.Patterns.Clear();
        }

        return MetadataGrid.Patterns.GetOrAdd(text, static t =>
        {
            try
            {
                return new Regex(t[1..^1], RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            catch (ArgumentException)
            {
                return null;
            }
        });
    }

    /// <summary>Whether <paramref name="value"/> passes a comparison such as <c>&gt;= 0x10</c>; null
    /// when <paramref name="text"/> is not one, as a bare number is not.</summary>
    private static bool? Compare(object value, string text)
    {
        var (op, operand) = text switch
        {
            _ when text.StartsWith(">=", StringComparison.Ordinal) => (">=", text[2..].Trim()),
            _ when text.StartsWith("<=", StringComparison.Ordinal) => ("<=", text[2..].Trim()),
            [('>' or '<' or '='), ..] => (text[..1], text[1..].Trim()),
            _ => (null, text),
        };
        if (op is null)
        {
            return null;
        }

        var parsed = operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number)
            : long.TryParse(operand, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
        if (!parsed)
        {
            return null;
        }

        long actual;
        try
        {
            actual = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is InvalidCastException or OverflowException or FormatException)
        {
            return null;
        }
        return op switch
        {
            "=" => actual == number,
            "<" => actual < number,
            ">" => actual > number,
            "<=" => actual <= number,
            _ => actual >= number,
        };
    }

    private static object? GetValue(MetadataGrid grid, int row, int column)
    {
        try
        {
            return grid.GridColumns[column].Value(grid.Rows[row], row + 1);
        }
        catch (Exception e) when (IsUnreadable(e))
        {
            return null;
        }
    }

    /// <summary>Whether <paramref name="e"/> is how metadata that cannot be read fails, which leaves
    /// its cell empty.</summary>
    private static bool IsUnreadable(Exception e)
    {
        return e is BadImageFormatException or ArgumentException or InvalidOperationException;
    }

    private static string FormatValue(object? value, string? format)
    {
        if (value is null)
        {
            return "";
        }

        if (!string.IsNullOrEmpty(format))
        {
            if (value is Enum enumValue)
            {
                value = Convert.ChangeType(enumValue, enumValue.GetTypeCode(), CultureInfo.InvariantCulture);
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(format, CultureInfo.InvariantCulture);
            }
        }
        return value.ToString() ?? "";
    }

    private static int Compare(object? a, object? b)
    {
        if (a is null || b is null)
        {
            return a is null ? b is null ? 0 : -1 : 1;
        }

        if (a.GetType() == b.GetType() && a is IComparable comparable)
        {
            return comparable.CompareTo(b);
        }

        if (IsNumber(a) && IsNumber(b))
        {
            return Convert.ToDecimal(a, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(b, CultureInfo.InvariantCulture));
        }

        return string.CompareOrdinal(a.ToString(), b.ToString());
    }

    private static bool IsNumber(object value)
    {
        return value is Enum || Type.GetTypeCode(value.GetType()) is >= TypeCode.SByte and <= TypeCode.Decimal;
    }

    private static uint GetBits(object value)
    {
        return unchecked((uint)Convert.ToInt64(value, CultureInfo.InvariantCulture));
    }
}
