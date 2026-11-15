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

    public required IReadOnlyList<GridColumn> Columns { get; init; }

    /// <summary>The column naming each row, or -1.</summary>
    internal int NameColumn { get; init; } = -1;

    /// <summary>The read half of what is shown under a selected row, for a grid that shows something: what it reads of
    /// the row through the library, its facts, inside the row's catch; null when the row has nothing to show.</summary>
    internal Func<object, object?>? Details { get; init; }

    /// <summary>The format half of what is shown under a selected row: the facts <see cref="Details"/> read made into
    /// it by the app's rules, after the read's outermost catch, so a bug in it is no unreadable row.</summary>
    internal Func<object, MetadataRowDetails?>? FormatDetails { get; init; }
}

/// <summary>A <see cref="GridSchema"/> over rows of <typeparamref name="TRow"/>, made by a
/// <see cref="GridSchemaBuilder{TRow}"/>.</summary>
public sealed class GridSchema<TRow> : GridSchema
{
    public required MetadataFile File { get; init; }
}
