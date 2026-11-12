using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.Tests;

internal static class MetadataQueries
{
    /// <summary>The table named <paramref name="name"/> in a metadata tables folder.</summary>
    public static async Task<BrowseNode> TableAsync(this BrowseNode tables, string name)
    {
        return (await tables.ListChildrenAsync()).Single(c => c.Text.Split(' ')[1] == name);
    }

    public static int Column(this MetadataGrid grid, string name)
    {
        var names = grid.Columns.Select(c => c.Name).ToList();
        return names.IndexOf(name);
    }

    public static int RowNamed(this MetadataGrid grid, string name)
    {
        var column = grid.Column("Name");
        for (var row = 0; row < grid.RowCount; row++)
        {
            var text = MetadataGrids.GetText(grid, row, column);
            if (text == name)
            {
                return row;
            }
        }
        throw new InvalidOperationException($"No row named {name}");
    }
}
