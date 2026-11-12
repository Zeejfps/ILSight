using System.Diagnostics;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

/// <summary>Reading the cells of a <see cref="MetadataGrid"/>: their text, links, tooltips and details, each worked out
/// once per cell viewed and kept; and the rows a sort and filters arrange, over the cells' values alone.</summary>
public static class MetadataGrids
{
    /// <summary>How many rows a sort or a filter goes through between looks at its token.</summary>
    private const int RowsBetweenChecks = 4096;

    /// <summary>The grid titled <paramref name="title"/> of what <paramref name="schema"/> reads from
    /// <paramref name="file"/>, which is <paramref name="module"/>'s own metadata or its debug metadata. For a module
    /// whose image is in memory, each cell is read when it is needed; for one whose file the library maps, every cell,
    /// tooltip, link, detail and definition is read now (<see cref="MetadataGrid.ReadWhole"/>), so the draw reads
    /// values. Call it off the screen's thread, as the node's content is made.</summary>
    public static MetadataGrid CreateGrid(string title, LoadedModule module, MetadataFile file, GridSchema schema)
    {
        var columns = schema.Columns;
        var rows = schema.ReadRows();
        var shown = columns.Select(static c => c.Column).ToArray();
        if (module.IsImageInMemory)
        {
            return new MetadataGrid.InMemory
            {
                Title = title,
                Module = module,
                File = file,
                Schema = schema,
                Rows = rows,
                GridColumns = columns,
                Columns = shown,
            };
        }

        var grid = new MetadataGrid.ReadWhole
        {
            Title = title,
            Module = module,
            Schema = schema,
            Rows = rows,
            GridColumns = columns,
            Columns = shown,
            Values = ReadValues(rows, columns),
        };
        ReadAllButValues(grid, file);
        return grid;
    }

    /// <summary>Every cell's value, by its index (row × columns + column), or its failure.</summary>
    private static object?[] ReadValues(IReadOnlyList<object> rows, IReadOnlyList<GridColumn> columns)
    {
        var values = new object?[rows.Count * columns.Count];
        for (var row = 0; row < rows.Count; row++)
        {
            for (var column = 0; column < columns.Count; column++)
            {
                values[row * columns.Count + column] = ReadValue(columns[column], rows[row], row);
            }
        }
        return values;
    }

    /// <summary>Keeps every tooltip, link, detail and definition of <paramref name="grid"/>'s cells and rows that has
    /// something to show, read through <paramref name="file"/>; what is not kept has nothing.</summary>
    private static void ReadAllButValues(MetadataGrid.ReadWhole grid, MetadataFile file)
    {
        var columns = grid.GridColumns.Count;
        for (var row = 0; row < grid.RowCount; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var index = row * columns + column;
                KeepFound(grid.Tooltips, index, ReadTooltip(grid, row, column));
                var link = grid.GridColumns[column].Column.IsToken ? ReadLink(grid, file, row, column) : null;
                if (link is not null)
                {
                    grid.Links[index] = link;
                }
            }
            KeepFound(grid.DetailCells, row, ReadDetails(grid, row));
            KeepFound(grid.Definitions, row, ReadDefinition(grid, file, row));
        }
    }

    private static void KeepFound<T>(Dictionary<int, T?> kept, int index, T? found)
        where T : class
    {
        if (found is null)
        {
            return;
        }

        kept[index] = found;
    }

    /// <summary>The text of a cell: numbers in the column's format, often hex; "?" for a cell whose read failed.</summary>
    public static string GetText(MetadataGrid grid, int row, int column)
    {
        var index = row * grid.GridColumns.Count + column;
        if (grid.TextCells.TryGetValue(index, out var known))
        {
            return known;
        }

        var text = FormatValue(FindValue(grid, row, column), grid.GridColumns[column].Format);
        grid.TextCells[index] = text;
        return text;
    }

    /// <summary>The rows a grid shows sorted by <paramref name="sortColumn"/> (none when negative), descending when
    /// <paramref name="descending"/>, that pass every one of <paramref name="filters"/>. <paramref name="order"/>, when
    /// given, is that sort, made before. Reads the cells' values alone, never what the grid keeps for the screen, so it
    /// may run off the screen's thread; give it filters of its own (<see cref="CopyFilters"/>).</summary>
    public static MetadataGridView ArrangeRows(MetadataGrid grid, int sortColumn, bool descending, int[]? order,
        IReadOnlyList<MetadataColumnFilter?> filters, CancellationToken cancellationToken)
    {
        var sorted = order;
        if (sorted is null && sortColumn >= 0)
        {
            sorted = OrderRows(grid, sortColumn, descending, cancellationToken);
        }

        return new MetadataGridView { Order = sorted, Rows = FilterRows(grid, sorted, filters, cancellationToken) };
    }

    /// <summary>A copy of <paramref name="filters"/> as they stand, for a filter to read while they change.</summary>
    public static MetadataColumnFilter?[] CopyFilters(IReadOnlyList<MetadataColumnFilter?> filters)
    {
        var copies = new MetadataColumnFilter?[filters.Count];
        for (var i = 0; i < copies.Length; i++)
        {
            copies[i] = filters[i]?.Copy();
        }
        return copies;
    }

    /// <summary>The rows of <paramref name="order"/>, or of the table in its own order, that pass
    /// every one of <paramref name="filters"/>, one per column or null. Reads the cells' values alone.</summary>
    public static int[] FilterRows(MetadataGrid grid, int[]? order, IReadOnlyList<MetadataColumnFilter?> filters, CancellationToken cancellationToken = default)
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

        var passed = new List<int>();
        for (var i = 0; i < rows.Length; i++)
        {
            if (i % RowsBetweenChecks == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var row = rows[i];
            if (active.TrueForAll(a => MatchesCell(grid, row, a.Column, a.Filter)))
            {
                passed.Add(row);
            }
        }
        return passed.ToArray();
    }

    /// <summary>What is shown under <paramref name="row"/> while it is selected; null when it has
    /// nothing; why when reading it failed.</summary>
    public static MetadataRowDetails? FindDetails(MetadataGrid grid, int row)
    {
        if (!grid.HasDetails)
        {
            return null;
        }

        if (grid.DetailCells.TryGetValue(row, out var known))
        {
            return known;
        }

        if (grid is not MetadataGrid.InMemory)
        {
            // Read whole: every row's details that had something to show were kept.
            return null;
        }

        var result = ReadDetails(grid, row);
        grid.DetailCells[row] = result;
        return result;
    }

    private static MetadataRowDetails? ReadDetails(MetadataGrid grid, int row)
    {
        var details = grid.Schema.Details;
        if (details is null)
        {
            return null;
        }

#pragma warning disable RL0902 // The schema's delegate reads ICSharpCode.Decompiler's metadata (a listed untrusted library): its failure is the row's.
        try
        {
            return details(grid.Rows[row]);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new MetadataRowDetails.Failed(e.Message);
        }
#pragma warning restore RL0902
    }

    /// <summary>A cell's text up to its first line break, and how many lines follow it — none for
    /// text on one line.</summary>
    public static (string FirstLine, int MoreLines) GetLines(MetadataGrid grid, int row, int column)
    {
        var index = row * grid.GridColumns.Count + column;
        if (grid.LineCells.TryGetValue(index, out var known))
        {
            return known;
        }

        var text = GetText(grid, row, column).TrimEnd('\r', '\n');
        var end = text.IndexOf('\n');
        var result = end < 0 ? (text, 0) : (text[..end].TrimEnd('\r'), text.AsSpan(end).Count('\n'));
        grid.LineCells[index] = result;
        return result;
    }

    /// <summary>The order that sorts the rows by <paramref name="column"/>, as row indices; ties keep the table's
    /// order, and cells whose read failed sort as empty ones. Reads the cells' values alone.</summary>
    public static int[] OrderRows(MetadataGrid grid, int column, bool descending, CancellationToken cancellationToken = default)
    {
        var keys = new object?[grid.Rows.Count];
        for (var row = 0; row < keys.Length; row++)
        {
            if (row % RowsBetweenChecks == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var value = FindValue(grid, row, column);
            keys[row] = value is MetadataCellFailure ? null : value;
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

    /// <summary>What hovering a cell explains: why its read failed, else what its column says of it; null when it
    /// has nothing to say.</summary>
    public static MetadataTooltip? FindTooltip(MetadataGrid grid, int row, int column)
    {
        var index = row * grid.GridColumns.Count + column;
        if (grid.Tooltips.TryGetValue(index, out var known))
        {
            return known;
        }

        if (grid is not MetadataGrid.InMemory)
        {
            // Read whole: every cell's tooltip that had something to say was kept.
            return null;
        }

        var result = ReadTooltip(grid, row, column);
        grid.Tooltips[index] = result;
        return result;
    }

    private static MetadataTooltip? ReadTooltip(MetadataGrid grid, int row, int column)
    {
        var value = FindValue(grid, row, column);
        if (value is MetadataCellFailure failed)
        {
            return new MetadataTooltip { Text = failed.Reason, Groups = [] };
        }

        var info = grid.GridColumns[column];
        if (info.Column.Flags is null && info.Tooltip is null)
        {
            return null;
        }

        object? given;
#pragma warning disable RL0902 // The column's tooltip delegate reads ICSharpCode.Decompiler's metadata (a listed untrusted library): its failure is the cell's.
        try
        {
            given = info.Column.Flags is not null
                ? ExplainFlags(info.Column.Flags, value)
                : info.Tooltip?.Invoke(grid.Rows[row], row + 1);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new MetadataTooltip { Text = e.Message, Groups = [] };
        }
#pragma warning restore RL0902
        return given switch
        {
            MetadataTooltip tooltip => tooltip,
            string text when !string.IsNullOrWhiteSpace(text) => new MetadataTooltip { Text = text, Groups = [] },
            _ => null,
        };
    }

    /// <summary>The row a token cell points at, in the table of this metadata it names; null for a
    /// cell that is not a token, a nil token or a table this metadata lists no rows of. A row past
    /// the table's end, as the end of an empty member list is, leads to the last row.</summary>
    public static MetadataAddress? FindLink(MetadataGrid grid, int row, int column)
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

        if (grid is not MetadataGrid.InMemory inMemory)
        {
            // Read whole: every link was kept.
            return null;
        }

        var result = ReadLink(grid, inMemory.File, row, column);
        grid.Links[index] = result;
        return result;
    }

    private static MetadataAddress? ReadLink(MetadataGrid grid, MetadataFile file, int row, int column)
    {
        var value = FindValue(grid, row, column);
        if (value is not int token || (token & 0xFFFFFF) == 0)
        {
            return null;
        }

        var table = (TableIndex)(token >>> 24);
        if (!IsTableListed(grid, file, table))
        {
            return null;
        }

        var count = file.Metadata.GetTableRowCount(table);
        return new MetadataAddress { Table = table, Row = Math.Min(token & 0xFFFFFF, count) - 1 };
    }

    /// <summary>Whether this metadata's tables list <paramref name="table"/> as a grid of rows, as they
    /// do a table they can read that has rows.</summary>
    private static bool IsTableListed(MetadataGrid grid, MetadataFile file, TableIndex table)
    {
        if (grid.Listed.TryGetValue(table, out var known))
        {
            return known;
        }

        var listed = Enum.IsDefined(table) && file.Metadata.GetTableRowCount(table) > 0 && MetadataTables.Create(table, file) is not null;
        grid.Listed[table] = listed;
        return listed;
    }

    /// <summary>The row of <paramref name="token"/>'s table it is; null when this does not list
    /// that table.</summary>
    public static int? FindRow(MetadataGrid grid, int token)
    {
        if (grid.Schema.Table is null || (int)grid.Schema.Table != token >>> 24)
        {
            return null;
        }

        return (token & 0xFFFFFF) - 1;
    }

    /// <summary>
    /// What the row stands for in the decompiled code, as the double-click on it goes to: the type,
    /// member or assembly it is — a reference resolved to its definition. Null for a row that is
    /// none, or a row of debug metadata.
    /// </summary>
    public static CodeReference.Navigable? FindDefinition(MetadataGrid grid, int row)
    {
        if (grid.Definitions.TryGetValue(row, out var known))
        {
            return known;
        }

        if (grid is not MetadataGrid.InMemory inMemory)
        {
            // Read whole: every row's definition was kept.
            return null;
        }

        var result = ReadDefinition(grid, inMemory.File, row);
        grid.Definitions[row] = result;
        return result;
    }

    private static CodeReference.Navigable? ReadDefinition(MetadataGrid grid, MetadataFile file, int row)
    {
        var own = LoadedAssemblies.GetFile(grid.Module);
        if (!ReferenceEquals(file, own) || grid.Schema.Table is null)
        {
            return null;
        }

        var token = (int)grid.Schema.Table << 24 | row + 1;
        var handle = MetadataTokens.EntityHandle(token);
        var name = handle.Kind == HandleKind.AssemblyDefinition ? grid.Module.Name
            : grid.Schema.NameColumn >= 0 ? FormatValue(FindValue(grid, row, grid.Schema.NameColumn), grid.GridColumns[grid.Schema.NameColumn].Format)
            : token.ToString("X8");
        return CodeReferences.Find(file.Metadata, handle, name, AssemblyPath.Of(file.FileName));
    }

    private static bool MatchesCell(MetadataGrid grid, int row, int column, MetadataColumnFilter filter)
    {
        var value = FindValue(grid, row, column);
        if (value is null || value is MetadataCellFailure)
        {
            return false;
        }

        return filter.Match((grid, column, value),
            static (s, text) => MatchesText(s.grid, s.column, s.value, text.Text),
            static (s, flags) => flags.Flags.IsEmpty || FilterStatePresenter.MatchesFilter(flags.Flags, GetBits(s.value)));
    }

    private static bool MatchesText(MetadataGrid grid, int column, object value, string text)
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

        var info = grid.GridColumns[column];
        if (info.Column.IsNumeric)
        {
            var compared = Compare(value, text);
            if (compared is not null)
            {
                return compared.Value;
            }
        }
        return FormatValue(value, info.Format).Contains(text, StringComparison.OrdinalIgnoreCase);
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
        catch (InvalidCastException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
        catch (FormatException)
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

    /// <summary>A cell's value: read through the file in memory, or as read whole; a <see cref="MetadataCellFailure"/>
    /// for one whose read failed.</summary>
    private static object? FindValue(MetadataGrid grid, int row, int column)
    {
        if (grid is MetadataGrid.ReadWhole whole)
        {
            return whole.Values[row * grid.GridColumns.Count + column];
        }

        return ReadValue(grid.GridColumns[column], grid.Rows[row], row);
    }

    private static object? ReadValue(GridColumn column, object row, int index)
    {
#pragma warning disable RL0902 // The column's delegate reads ICSharpCode.Decompiler's metadata (a listed untrusted library): its failure is the cell's.
        try
        {
            return column.Value(row, index + 1);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new MetadataCellFailure { Reason = e.Message };
        }
#pragma warning restore RL0902
    }

    private static string FormatValue(object? value, string? format)
    {
        if (value is null)
        {
            return "";
        }

        if (value is MetadataCellFailure)
        {
            return "?";
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

        var aType = a.GetType();
        var bType = b.GetType();
        if (aType == bType && a is IComparable comparable)
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
