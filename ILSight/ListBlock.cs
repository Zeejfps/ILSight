namespace ILSight;

/// <summary>A list's rows, each a lead and its content: a marker and the item, or a table's
/// term and its description.</summary>
internal sealed record ListBlock : TooltipBlock
{
    public required bool IsTable { get; init; }
    public required IReadOnlyList<ListRow> Rows { get; init; }
}
