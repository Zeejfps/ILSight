using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

/// <summary>A <see cref="GridSchema{TRow}"/> being built a column at a time by <see cref="GridSchemas"/>; one per
/// schema.</summary>
public sealed class GridSchemaBuilder<TRow>
{
    private readonly List<GridColumn> _columns = [];
    private int _nameColumn = -1;
    private Func<object, MetadataRowDetails?>? _details;

    /// <summary>What names the kind of rows, the same for a table in any assembly.</summary>
    public required string Key { get; init; }

    /// <summary>The table the rows are of, for a metadata table.</summary>
    public required TableIndex? Table { get; init; }

    public required MetadataFile File { get; init; }

    /// <summary>Reads every row, in order.</summary>
    public required Func<IReadOnlyList<object>> ReadRows { get; init; }

    public void AddColumn(GridColumn column)
    {
        _columns.Add(column);
    }

    /// <summary>Takes the column added next as the one naming each row.</summary>
    public void NameNextColumn()
    {
        _nameColumn = _columns.Count;
    }

    /// <summary>Takes <paramref name="details"/> as what is shown under a selected row.</summary>
    public void KeepDetails(Func<object, MetadataRowDetails?> details)
    {
        _details = details;
    }

    public GridSchema<TRow> ToSchema()
    {
        return new GridSchema<TRow>
        {
            Key = Key,
            Table = Table,
            File = File,
            ReadRows = ReadRows,
            Columns = _columns.ToArray(),
            NameColumn = _nameColumn,
            Details = _details,
        };
    }
}
