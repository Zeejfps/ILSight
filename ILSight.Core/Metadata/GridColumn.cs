// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

namespace ILSight.Core.Metadata;

/// <summary>One column of a <see cref="GridSchema"/>: its cell value and tooltip, read from a row
/// and its 1-based RID.</summary>
public sealed record GridColumn(MetadataColumn Column, string? Format, Func<object, int, object?> Value, Func<object, int, object?>? Tooltip);
