// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

namespace ILSight.Core.Metadata;

/// <summary>One column of a <see cref="GridSchema"/>: its cell value and tooltip, read from a row
/// and its 1-based RID.</summary>
public sealed record GridColumn
{
    public required MetadataColumn Column { get; init; }

    public required string? Format { get; init; }

    public required Func<object, int, object?> Value { get; init; }

    /// <summary>The read half of the cell's tooltip: what it reads of the row through the library, its facts, inside the
    /// cell's catch; null when the column explains nothing.</summary>
    public required Func<object, int, object?>? Tooltip { get; init; }

    /// <summary>The format half of the cell's tooltip: the facts <see cref="Tooltip"/> read made into what the cell
    /// explains by the app's rules, after the read's outermost catch; null when the facts are the tooltip or its
    /// text.</summary>
    public Func<object, MetadataTooltip?>? FormatTooltip { get; init; }
}
