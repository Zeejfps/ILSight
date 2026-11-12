// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

namespace ILSight.Core.Metadata;

/// <summary>One column of a <see cref="GridSchema"/>: its cell value and tooltip, read from a row
/// and its 1-based RID.</summary>
public sealed record GridColumn
{
    public required MetadataColumn Column { get; init; }

    public required string? Format { get; init; }

    public required Func<object, int, object?> Value { get; init; }

    public required Func<object, int, object?>? Tooltip { get; init; }
}
