using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

/// <summary>
/// The rows of a metadata node as ILSpy shows them in a grid: a header, a directory, a table or a heap, read through
/// its <see cref="GridSchema"/>, one of two ways by what reading a cell can reach. <see cref="InMemory"/>: its file was
/// read whole into memory, so a cell is read where it is drawn, its failure shown in its place. <see cref="Paged"/>:
/// its file is one the library maps (a WebCIL module's), each read reaching the disk, so its cells, tooltips, links,
/// details and definitions are read a page of rows at a time on the I/O side (<see cref="MetadataGrids.ReadPage"/>),
/// the first page as the grid is made, and the draw shows the pages read. Cell text is worked out once per cell viewed
/// and kept; read the grid on one thread at a time (the screen's). Sorting and filtering
/// (<see cref="MetadataGrids.ArrangeRows"/>) read the cells' values through the file alone, never what is kept, so they
/// run off it.
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

    /// <summary>Whether what <paramref name="row"/> shows has been read: always for a grid whose file is in memory;
    /// for a paged one, once its page has.</summary>
    public abstract bool IsRowRead(int row);

    /// <summary>A cell's value as the screen shows it, a <see cref="MetadataCellFailure"/> for one whose read failed;
    /// null for a row not read (<see cref="IsRowRead"/>).</summary>
    internal abstract object? FindValue(int row, int column);

    /// <summary>What hovering a cell explains; null when it has nothing to say, or its row was not read.</summary>
    internal abstract MetadataTooltip? FindTooltip(int row, int column);

    /// <summary>The row a token cell points at; null when it points at none, or its row was not read.</summary>
    internal abstract MetadataAddress? FindLink(int row, int column);

    /// <summary>What is shown under <paramref name="row"/> while it is selected; null when it has nothing, or it was
    /// not read.</summary>
    internal abstract MetadataRowDetails? FindDetails(int row);

    /// <summary>What <paramref name="row"/> stands for in the code; null when it is none, or it was not read.</summary>
    internal abstract CodeReference.Navigable? FindDefinition(int row);

    /// <summary>A grid whose file is in memory: each cell is read through it where it is needed, a draw
    /// included, and what was read is kept.</summary>
    public sealed class InMemory : MetadataGrid
    {
        private readonly Dictionary<int, MetadataTooltip?> _tooltips = new();
        private readonly Dictionary<int, MetadataAddress?> _links = new();
        private readonly Dictionary<TableIndex, bool> _listed = new();
        private readonly Dictionary<int, MetadataRowDetails?> _details = new();
        private readonly Dictionary<int, CodeReference.Navigable?> _definitions = new();

        public required MetadataFile File { get; init; }

        public override bool IsRowRead(int row)
        {
            return true;
        }

        internal override object? FindValue(int row, int column)
        {
            return MetadataGrids.ReadValue(this, row, column);
        }

        internal override MetadataTooltip? FindTooltip(int row, int column)
        {
            var index = row * GridColumns.Count + column;
            if (_tooltips.TryGetValue(index, out var known))
            {
                return known;
            }

            var result = MetadataGrids.ReadTooltip(this, row, column, MetadataGrids.ReadValue(this, row, column));
            _tooltips[index] = result;
            return result;
        }

        internal override MetadataAddress? FindLink(int row, int column)
        {
            var index = row * GridColumns.Count + column;
            if (_links.TryGetValue(index, out var known))
            {
                return known;
            }

            var result = MetadataGrids.ReadLink(File, _listed, MetadataGrids.ReadValue(this, row, column));
            _links[index] = result;
            return result;
        }

        internal override MetadataRowDetails? FindDetails(int row)
        {
            if (_details.TryGetValue(row, out var known))
            {
                return known;
            }

            var result = MetadataGrids.ReadDetails(this, row);
            _details[row] = result;
            return result;
        }

        internal override CodeReference.Navigable? FindDefinition(int row)
        {
            if (_definitions.TryGetValue(row, out var known))
            {
                return known;
            }

            var result = MetadataGrids.ReadDefinition(this, File, row);
            _definitions[row] = result;
            return result;
        }
    }

    /// <summary>A grid whose file the library maps: its rows are read a page at a time, on the I/O side
    /// (<see cref="MetadataGrids.ReadPage"/>), each cell's value or <see cref="MetadataCellFailure"/> with its tooltip
    /// and link, each row's details and definition. The draw shows the pages read and asks for those it finds missing
    /// (<see cref="Ask"/>), once each, for the frame edge to read.</summary>
    public sealed class Paged : MetadataGrid
    {
        /// <summary>How many rows a page holds, the size of each read the frame edge makes for the draw. The first
        /// read, as the grid is made, takes as many pages as its first show needs
        /// (<see cref="MetadataGrids.CreateGrid"/>).</summary>
        public const int RowsPerPage = 64;

        private readonly Dictionary<int, MetadataGridPage> _pages = new();
        private readonly HashSet<int> _asked = new();

        /// <summary>The mapped file, read on the I/O side alone.</summary>
        public required MetadataFile File { get; init; }

        /// <summary>The page <paramref name="row"/> is on.</summary>
        public static int PageOf(int row)
        {
            return row / RowsPerPage;
        }

        /// <summary>How many pages the first <paramref name="rows"/> rows are on.</summary>
        public static int CountPages(int rows)
        {
            return (rows + RowsPerPage - 1) / RowsPerPage;
        }

        public override bool IsRowRead(int row)
        {
            return _pages.ContainsKey(PageOf(row));
        }

        /// <summary>Asks for the page <paramref name="row"/> is on to be read. Returns whether that is new: false when
        /// it was read, or asked for already.</summary>
        public bool Ask(int row)
        {
            if (IsRowRead(row))
            {
                return false;
            }

            return _asked.Add(PageOf(row));
        }

        /// <summary>Keeps <paramref name="read"/>, the page at <paramref name="page"/>, which the draw shows from now
        /// on.</summary>
        public void AddPage(int page, MetadataGridPage read)
        {
            _pages[page] = read;
            _asked.Remove(page);
        }

        internal override object? FindValue(int row, int column)
        {
            var page = _pages.GetValueOrDefault(PageOf(row));
            if (page is null)
            {
                return null;
            }

            return page.Values[row % RowsPerPage * GridColumns.Count + column];
        }

        internal override MetadataTooltip? FindTooltip(int row, int column)
        {
            var page = _pages.GetValueOrDefault(PageOf(row));
            if (page is null)
            {
                return null;
            }

            return page.Tooltips[row % RowsPerPage * GridColumns.Count + column];
        }

        internal override MetadataAddress? FindLink(int row, int column)
        {
            var page = _pages.GetValueOrDefault(PageOf(row));
            if (page is null)
            {
                return null;
            }

            return page.Links[row % RowsPerPage * GridColumns.Count + column];
        }

        internal override MetadataRowDetails? FindDetails(int row)
        {
            var page = _pages.GetValueOrDefault(PageOf(row));
            if (page is null)
            {
                return null;
            }

            return page.Details[row % RowsPerPage];
        }

        internal override CodeReference.Navigable? FindDefinition(int row)
        {
            var page = _pages.GetValueOrDefault(PageOf(row));
            if (page is null)
            {
                return null;
            }

            return page.Definitions[row % RowsPerPage];
        }
    }

    public TResult Match<TResult>(Func<InMemory, TResult> inMemory, Func<Paged, TResult> paged)
    {
        return this switch
        {
            InMemory m => inMemory(m),
            Paged p => paged(p),
            _ => throw new UnreachableException($"MetadataGrid.Match does not handle {GetType().Name}."),
        };
    }
}
