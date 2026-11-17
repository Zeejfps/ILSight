using System.Diagnostics;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.MetadataArea;

/// <summary>Reading the cells of a <see cref="MetadataGrid"/>: their text, links, tooltips and details, each worked out
/// once per cell viewed and kept, or read with its page; the pages of a paged grid, on the I/O side; and the rows a sort
/// and filters arrange, over the cells' values alone.</summary>
public static class MetadataGrids
{
    private const int RowsBetweenChecks = 4096;

    /// <summary>The grid titled <paramref name="title"/> of what <paramref name="schema"/> reads from
    /// <paramref name="file"/>, which is <paramref name="module"/>'s own metadata or its debug metadata. For a module
    /// whose image is in memory, each cell is read when it is needed; for one whose file the library maps, the grid is
    /// paged (<see cref="MetadataGrid.Paged"/>), and the pages of its first <paramref name="firstRows"/> rows are read
    /// now: what its first show needs, which the app names (the window its view opens to, and the rows it measures its
    /// columns over), so that it never shows those rows loading. What the library throws reading the rows or those
    /// pages is the grid's <see cref="Outcome{T}.Failed"/>; the grid is built of what was read after each read's catch,
    /// so a bug of the app's in building it reaches the caller. Call it off the screen's thread, as the node's content
    /// is made.</summary>
    public static Outcome<MetadataGrid> CreateGrid(string title, LoadedModule module, MetadataFile file, GridSchema schema, int firstRows,
        CancellationToken ct)
    {
        IReadOnlyList<object> rows;
        try
        {
            rows = schema.ReadRows();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<MetadataGrid>.Failed(e);
        }

        var columns = schema.Columns;
        var shown = columns.Select(static c => c.Column).ToArray();
        if (module.IsImageInMemory)
        {
            return new Outcome<MetadataGrid>.Ok(new MetadataGrid.InMemory
            {
                Title = title,
                Module = module,
                File = file,
                Schema = schema,
                Rows = rows,
                GridColumns = columns,
                Columns = shown,
            });
        }

        var grid = new MetadataGrid.Paged
        {
            Title = title,
            Module = module,
            File = file,
            Schema = schema,
            Rows = rows,
            GridColumns = columns,
            Columns = shown,
        };
        var pages = MetadataGrid.Paged.CountPages(Math.Min(firstRows, rows.Count));
        var read = new List<ReadGridPage>(pages);
        try
        {
            for (var page = 0; page < pages; page++)
            {
                read.Add(ReadPageUnderReading(grid, page));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<MetadataGrid>.Failed(e);
        }

        // The app's formatting of what was read, after the read's catch.
        for (var page = 0; page < read.Count; page++)
        {
            grid.AddPage(page, MakePage(grid, read[page]));
        }

        return new Outcome<MetadataGrid>.Ok(grid);
    }

    /// <summary>The rows of <paramref name="grid"/> on the page at <paramref name="page"/>, read through its mapped
    /// file: each cell's value, tooltip and link, each row's details and definition, what the library throws reading
    /// one the cell's or row's failure, in its place; what else it throws reading the page (a link's table, a row's
    /// definition), the page's <see cref="Outcome{T}.Failed"/>. A unit reading the grid's module: closed, reading
    /// nothing, when it was released (closed) before. I/O: call it off the screen's thread.</summary>
    public static Outcome<ModuleRead<MetadataGridPage>> ReadPage(MetadataGrid.Paged grid, int page)
    {
        using var reading = LoadedAssemblies.TryBeginReading(grid.Module);
        if (reading is null)
        {
            return new Outcome<ModuleRead<MetadataGridPage>>.Ok(new ModuleRead<MetadataGridPage>.Closed { Module = grid.Module });
        }

        ReadGridPage read;
        try
        {
            read = ReadPageUnderReading(grid, page);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<ModuleRead<MetadataGridPage>>.Failed(e);
        }

        // The app's formatting of what was read, after the read's outermost catch: a bug of the app's is no unreadable page.
        return new Outcome<ModuleRead<MetadataGridPage>>.Ok(new ModuleRead<MetadataGridPage>.Read { Value = MakePage(grid, read) });
    }

    private static ReadGridPage ReadPageUnderReading(MetadataGrid.Paged grid, int page)
    {
        var first = page * MetadataGrid.Paged.RowsPerPage;
        var rows = Math.Clamp(grid.RowCount - first, 0, MetadataGrid.Paged.RowsPerPage);
        var columns = grid.GridColumns.Count;
        var values = new object?[rows * columns];
        var tooltips = new object?[rows * columns];
        var links = new MetadataAddress?[rows * columns];
        var details = new object?[rows];
        var definitions = new CodeReference.Navigable?[rows];
        var listed = new Dictionary<TableIndex, bool>();
        for (var r = 0; r < rows; r++)
        {
            var row = first + r;
            for (var column = 0; column < columns; column++)
            {
                var index = r * columns + column;
                var value = ReadCell(grid, row, column);
                values[index] = value;
                tooltips[index] = ReadTooltip(grid, row, column, value);
                links[index] = grid.GridColumns[column].Column.IsToken ? ReadLink(grid.File, listed, value) : null;
            }

            details[r] = ReadDetails(grid, row);
            definitions[r] = ReadDefinition(grid, grid.File, row);
        }

        return new ReadGridPage { Values = values, Tooltips = tooltips, Links = links, Details = details, Definitions = definitions };
    }

    // The app's formatting of a page as read, after the read's outermost catch: each cell's value and tooltip, each row's
    // details.
    private static MetadataGridPage MakePage(MetadataGrid grid, ReadGridPage read)
    {
        var columns = grid.GridColumns.Count;
        var values = new object?[read.Values.Length];
        var tooltips = new MetadataTooltip?[read.Tooltips.Length];
        for (var index = 0; index < tooltips.Length; index++)
        {
            values[index] = FormatCell(grid, index % columns, read.Values[index]);
            tooltips[index] = MakeTooltip(grid, index % columns, read.Tooltips[index]);
        }

        var details = new MetadataRowDetails?[read.Details.Length];
        for (var row = 0; row < details.Length; row++)
        {
            details[row] = MakeDetails(grid, read.Details[row]);
        }

        return new MetadataGridPage { Values = values, Tooltips = tooltips, Links = read.Links, Details = details, Definitions = read.Definitions };
    }

    /// <summary>The page at <paramref name="page"/> of <paramref name="grid"/> when reading it failed with
    /// <paramref name="error"/>: each of its cells that failure, in its place.</summary>
    public static MetadataGridPage FailedPage(MetadataGrid.Paged grid, int page, Exception error)
    {
        var rows = Math.Clamp(grid.RowCount - page * MetadataGrid.Paged.RowsPerPage, 0, MetadataGrid.Paged.RowsPerPage);
        var cells = rows * grid.GridColumns.Count;
        var failure = new MetadataCellFailure { Reason = error.Message, Error = error };
        var tooltip = new MetadataTooltip { Text = error.Message, Groups = [] };
        var details = grid.HasDetails ? new MetadataRowDetails.Failed { Reason = error.Message, Error = error } : (MetadataRowDetails?)null;
        return new MetadataGridPage
        {
            Values = Enumerable.Repeat<object?>(failure, cells).ToArray(),
            Tooltips = Enumerable.Repeat<MetadataTooltip?>(tooltip, cells).ToArray(),
            Links = new MetadataAddress?[cells],
            Details = Enumerable.Repeat(details, rows).ToArray(),
            Definitions = new CodeReference.Navigable?[rows],
        };
    }

    /// <summary>The text of a cell: numbers in the column's format, often hex; "?" for a cell whose read failed; empty
    /// for a row not read yet (<see cref="MetadataGrid.IsRowRead"/>).</summary>
    public static string GetText(MetadataGrid grid, int row, int column)
    {
        var index = row * grid.GridColumns.Count + column;
        if (grid.TextCells.TryGetValue(index, out var known))
        {
            return known;
        }

        if (!grid.IsRowRead(row))
        {
            return "";
        }

        var text = FormatValue(grid.FindValue(row, column), grid.GridColumns[column].Format);
        grid.TextCells[index] = text;
        return text;
    }

    /// <summary>The rows a grid shows sorted by <paramref name="sortColumn"/> (none when negative), descending when
    /// <paramref name="descending"/>, that pass every one of <paramref name="filters"/>. <paramref name="order"/>, when
    /// given, is that sort, made before. Reads the cells' values alone, through the grid's file, never what the grid keeps
    /// for the screen, so it runs off the screen's thread (for a paged grid, I/O); give it filters of its own
    /// (<see cref="CopyFilters"/>). A unit reading the grid's module: closed, arranging nothing, when it was released
    /// (closed) before.</summary>
    public static ModuleRead<MetadataGridView> ArrangeRows(MetadataGrid grid, int sortColumn, bool descending, int[]? order,
        IReadOnlyList<MetadataColumnFilter?> filters, CancellationToken cancellationToken)
    {
        using var reading = LoadedAssemblies.TryBeginReading(grid.Module);
        if (reading is null)
        {
            return new ModuleRead<MetadataGridView>.Closed { Module = grid.Module };
        }

        var sorted = order;
        if (sorted is null && sortColumn >= 0)
        {
            sorted = OrderRows(grid, sortColumn, descending, cancellationToken);
        }

        return new ModuleRead<MetadataGridView>.Read { Value = new MetadataGridView { Order = sorted, Rows = FilterRows(grid, sorted, filters, cancellationToken) } };
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
    /// every one of <paramref name="filters"/>, one per column or null. Reads the cells' values alone, through the
    /// grid's file: off the screen's thread for a paged grid.</summary>
    public static int[] FilterRows(MetadataGrid grid, int[]? order, IReadOnlyList<MetadataColumnFilter?> filters, CancellationToken cancellationToken = default)
    {
        var active = new List<(int Column, MetadataColumnFilter Filter)>();
        var columns = Math.Min(filters.Count, grid.GridColumns.Count);
        for (var c = 0; c < columns; c++)
        {
            var filter = filters[c];
            if (filter is not null && filter.Value.IsActive)
            {
                active.Add((c, filter.Value));
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
    /// nothing, or its row was not read yet; why when reading it failed.</summary>
    public static MetadataRowDetails? FindDetails(MetadataGrid grid, int row)
    {
        if (!grid.HasDetails)
        {
            return null;
        }

        return grid.FindDetails(row);
    }

    /// <summary>What the schema's read half reads under <paramref name="row"/> through the grid's file, its facts,
    /// inside the row's catch, which <see cref="MakeDetails"/> makes into what is shown after it; null when it has
    /// nothing; why, a <see cref="MetadataRowDetails.Failed"/>, when reading it failed.</summary>
    internal static object? ReadDetails(MetadataGrid grid, int row)
    {
        var details = grid.Schema.Details;
        if (details is null)
        {
            return null;
        }

        try
        {
            return details(grid.Rows[row]);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new MetadataRowDetails.Failed { Reason = e.Message, Error = e };
        }
    }

    /// <summary>The app's rules over what a row's details read (<see cref="ReadDetails"/>), after the read's outermost
    /// catch: nothing, a read that failed saying why, else the schema's format half makes the facts into what is
    /// shown.</summary>
    internal static MetadataRowDetails? MakeDetails(MetadataGrid grid, object? read)
    {
        // What the read half holds is the bare case, not a union: a failure is matched as the case it is.
        if (read is null)
        {
            return null;
        }

        if (read is MetadataRowDetails.Failed failed)
        {
            return failed;
        }

        var format = grid.Schema.FormatDetails ?? throw new UnreachableException("A schema that reads details makes them.");
        return format(read);
    }

    /// <summary>A cell's text up to its first line break, and how many lines follow it — none for
    /// text on one line, or a row not read yet.</summary>
    public static (string FirstLine, int MoreLines) GetLines(MetadataGrid grid, int row, int column)
    {
        var index = row * grid.GridColumns.Count + column;
        if (grid.LineCells.TryGetValue(index, out var known))
        {
            return known;
        }

        if (!grid.IsRowRead(row))
        {
            return ("", 0);
        }

        var text = GetText(grid, row, column).TrimEnd('\r', '\n');
        var end = text.IndexOf('\n');
        var result = end < 0 ? (text, 0) : (text[..end].TrimEnd('\r'), text.AsSpan(end).Count('\n'));
        grid.LineCells[index] = result;
        return result;
    }

    /// <summary>The order that sorts the rows by <paramref name="column"/>, as row indices; ties keep the table's
    /// order, and cells whose read failed sort as empty ones. Reads the cells' values alone, through the grid's file:
    /// off the screen's thread for a paged grid.</summary>
    public static int[] OrderRows(MetadataGrid grid, int column, bool descending, CancellationToken cancellationToken = default)
    {
        var keys = new object?[grid.Rows.Count];
        for (var row = 0; row < keys.Length; row++)
        {
            if (row % RowsBetweenChecks == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var value = ReadValue(grid, row, column);
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
    /// has nothing to say, or its row was not read yet.</summary>
    public static MetadataTooltip? FindTooltip(MetadataGrid grid, int row, int column)
    {
        return grid.FindTooltip(row, column);
    }

    /// <summary>What hovering the cell whose value is <paramref name="value"/> explains: read through the grid's file
    /// (<see cref="ReadTooltip"/>), then made (<see cref="MakeTooltip"/>), with no catch of its own around both.</summary>
    internal static MetadataTooltip? FindTooltip(MetadataGrid grid, int row, int column, object? value)
    {
        return MakeTooltip(grid, column, ReadTooltip(grid, row, column, value));
    }

    /// <summary>What the cell whose value is <paramref name="value"/> reads for its tooltip through the grid's file, its
    /// facts, inside the cell's catch: the value's failure as it is; nothing more for a flags column, whose value is
    /// its facts; else what the column's read half reads, or its failure, the cell's.</summary>
    internal static object? ReadTooltip(MetadataGrid grid, int row, int column, object? value)
    {
        if (value is MetadataCellFailure)
        {
            return value;
        }

        var info = grid.GridColumns[column];
        if (info.Column.Flags is not null)
        {
            return value;
        }

        if (info.Tooltip is null)
        {
            return null;
        }

        try
        {
            return info.Tooltip(grid.Rows[row], row + 1);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new MetadataCellFailure { Reason = e.Message, Error = e };
        }
    }

    // The app's rules over what the cell's tooltip read, after the read's outermost catch: a read that failed says why,
    // a flags column explains its bits, a column's format half makes its facts into the tooltip, else the facts are it
    // or its text.
    private static MetadataTooltip? MakeTooltip(MetadataGrid grid, int column, object? read)
    {
        if (read is MetadataCellFailure failed)
        {
            return new MetadataTooltip { Text = failed.Reason, Groups = [] };
        }

        var info = grid.GridColumns[column];
        if (info.Column.Flags is not null)
        {
            return ExplainFlags(info.Column.Flags, read);
        }

        if (read is null)
        {
            return null;
        }

        if (info.FormatTooltip is not null)
        {
            return info.FormatTooltip(read);
        }

        return read switch
        {
            MetadataTooltip tooltip => tooltip,
            string text when !string.IsNullOrWhiteSpace(text) => new MetadataTooltip { Text = text, Groups = [] },
            _ => null,
        };
    }

    /// <summary>The row a token cell points at, in the table of this metadata it names; null for a
    /// cell that is not a token, a nil token, a table this metadata lists no rows of, or a row not read yet. A row past
    /// the table's end, as the end of an empty member list is, leads to the last row.</summary>
    public static MetadataAddress? FindLink(MetadataGrid grid, int row, int column)
    {
        if (!grid.GridColumns[column].Column.IsToken)
        {
            return null;
        }

        return grid.FindLink(row, column);
    }

    /// <summary>The row the token <paramref name="value"/> points at in <paramref name="file"/>, which tables it
    /// lists kept in <paramref name="listed"/>.</summary>
    internal static MetadataAddress? ReadLink(MetadataFile file, Dictionary<TableIndex, bool> listed, object? value)
    {
        if (value is not int token || (token & 0xFFFFFF) == 0)
        {
            return null;
        }

        var table = (TableIndex)(token >>> 24);
        if (!IsTableListed(listed, file, table))
        {
            return null;
        }

        var count = file.Metadata.GetTableRowCount(table);
        return new MetadataAddress { Table = table, Row = Math.Min(token & 0xFFFFFF, count) - 1 };
    }

    private static bool IsTableListed(Dictionary<TableIndex, bool> listed, MetadataFile file, TableIndex table)
    {
        if (listed.TryGetValue(table, out var known))
        {
            return known;
        }

        var found = Enum.IsDefined(table) && file.Metadata.GetTableRowCount(table) > 0 && MetadataTables.Create(table, file) is not null;
        listed[table] = found;
        return found;
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
    /// none, a row of debug metadata, or a row not read yet.
    /// </summary>
    public static CodeReference.Navigable? FindDefinition(MetadataGrid grid, int row)
    {
        return grid.FindDefinition(row);
    }

    /// <summary>What <paramref name="row"/> stands for in the code, read through <paramref name="file"/>.</summary>
    internal static CodeReference.Navigable? ReadDefinition(MetadataGrid grid, MetadataFile file, int row)
    {
        if (!LoadedAssemblies.IsReadFrom(grid.Module, file) || grid.Schema.Table is null)
        {
            return null;
        }

        var token = (int)grid.Schema.Table << 24 | row + 1;
        var handle = MetadataTokens.EntityHandle(token);
        var name = handle.Kind == HandleKind.AssemblyDefinition ? grid.Module.Name
            : grid.Schema.NameColumn >= 0 ? FormatValue(ReadValue(grid, row, grid.Schema.NameColumn), grid.GridColumns[grid.Schema.NameColumn].Format)
            : token.ToString("X8");
        return CodeReferences.Find(file.Metadata, handle, name, AssemblyPath.Of(file.FileName));
    }

    private static bool MatchesCell(MetadataGrid grid, int row, int column, MetadataColumnFilter filter)
    {
        var value = ReadValue(grid, row, column);
        if (value is null || value is MetadataCellFailure)
        {
            return false;
        }

        return filter switch
        {
            MetadataColumnFilter.ByText text => MatchesText(grid, column, value, text.Text),
            MetadataColumnFilter.ByFlags flags => flags.Flags.IsEmpty || flags.Flags.Matches(GetBits(value)),
        };
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

    /// <summary>A cell's value, read through the grid's file (for a paged grid, I/O: off the screen's thread), then
    /// made into its text by the column's format half, if it has one, after the read's catch; a
    /// <see cref="MetadataCellFailure"/> for one whose read failed.</summary>
    internal static object? ReadValue(MetadataGrid grid, int row, int column)
    {
        return FormatCell(grid, column, ReadCell(grid, row, column));
    }

    // The read half of a cell's value, inside the cell's catch: what the column's delegate reads, or its failure.
    private static object? ReadCell(MetadataGrid grid, int row, int column)
    {
        var info = grid.GridColumns[column];
        try
        {
            return info.Value(grid.Rows[row], row + 1);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new MetadataCellFailure { Reason = e.Message, Error = e };
        }
    }

    // The format half of a cell's value, after the read's outermost catch (a paged grid's page's included): what was
    // read made into the cell's value by the column's format half, if it has one; nothing read, or a failed read, as it
    // is.
    private static object? FormatCell(MetadataGrid grid, int column, object? read)
    {
        var info = grid.GridColumns[column];
        return read is null or MetadataCellFailure || info.FormatValue is null ? read : info.FormatValue(read);
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

    private sealed class MetadataCellFailure
    {
        public required string Reason { get; init; }

        // What the library threw reading the cell: the diagnostic its record carries (CF3).
        public required Exception Error { get; init; }
    }

    // A page as read, inside its catch: each cell's value and tooltip and each row's details as read, which MakePage
    // formats after it.
    private sealed record ReadGridPage
    {
        public required object?[] Values { get; init; }

        public required object?[] Tooltips { get; init; }

        public required MetadataAddress?[] Links { get; init; }

        public required object?[] Details { get; init; }

        public required CodeReference.Navigable?[] Definitions { get; init; }
    }
}
