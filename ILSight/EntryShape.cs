namespace ILSight;

/// <summary>How a group sets out its entries: parameters with their names in a column,
/// exceptions with their descriptions under them, anything else as its blocks.</summary>
internal enum EntryShape
{
    Blocks,
    Column,
    Stacked,
}
