// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

/// <summary>What a <see cref="MetadataGrid"/> lists: its columns, and how to read its rows.</summary>
public class GridSchema
{
    /// <summary>What names the kind of rows, the same for a table in any assembly.</summary>
    public required string Key { get; init; }

    /// <summary>The table the rows are of, for a metadata table.</summary>
    public required TableIndex? Table { get; init; }

    /// <summary>Reads every row, in order.</summary>
    public required Func<IReadOnlyList<object>> ReadRows { get; init; }

    public readonly List<GridColumn> Columns = [];

    /// <summary>The column naming each row, or -1.</summary>
    internal int NameColumn = -1;

    /// <summary>What is shown under a selected row, for a grid that shows something.</summary>
    internal Func<object, MetadataRowDetails?>? Details;
}

/// <summary>A <see cref="GridSchema"/> over rows of <typeparamref name="TRow"/>, built a column at a
/// time by <see cref="GridSchemas"/>.</summary>
public sealed class GridSchema<TRow> : GridSchema
{
    public required MetadataFile File { get; init; }
}
