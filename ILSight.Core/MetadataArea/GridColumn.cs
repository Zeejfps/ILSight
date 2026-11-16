// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

namespace ILSight.Core.MetadataArea;

/// <summary>One column of a <see cref="GridSchema"/>: its cell value and tooltip, read from a row
/// and its 1-based RID.</summary>
public sealed record GridColumn
{
    public required MetadataColumn Column { get; init; }

    public required string? Format { get; init; }

    /// <summary>The read of the cell's value: what it reads of the row through the library, inside the cell's
    /// catch.</summary>
    public required GridCellRead Value { get; init; }

    /// <summary>The format half of the cell's value: what <see cref="Value"/> read made into the cell's one value (its
    /// text) by the app's rules, after the read's catch, so a bug in it is no unreadable cell; null when the read is the
    /// value. The grid sorts and filters by what it makes.</summary>
    public Func<object, object?>? FormatValue { get; init; }

    /// <summary>The read half of the cell's tooltip: what it reads of the row through the library, its facts, inside the
    /// cell's catch; null when the column explains nothing.</summary>
    public required GridCellRead? Tooltip { get; init; }

    /// <summary>The format half of the cell's tooltip: the facts <see cref="Tooltip"/> read made into what the cell
    /// explains by the app's rules, after the read's outermost catch; null when the facts are the tooltip or its
    /// text.</summary>
    public Func<object, MetadataTooltip?>? FormatTooltip { get; init; }
}
