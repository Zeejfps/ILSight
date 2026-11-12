using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.Tests;

public sealed class BlobTooltipTests
{
    private static async Task<MetadataGrid> GridAsync(string table)
    {
        var tables = await Trees.TestAssembly.ReadRootAsync().GetChildAsync("Metadata", "Tables");
        var node = await tables.TableAsync(table);
        return Assert.IsType<NodeContent.Table>(await node.ReadContentAsync()).Grid;
    }

    private static string? TooltipText(MetadataGrid grid, int row, string column)
    {
        return MetadataGrids.FindTooltip(grid, row, grid.Column(column))?.Text;
    }

    private static List<string?> ListTooltipTexts(MetadataGrid grid, string column)
    {
        return Enumerable.Range(0, grid.RowCount)
            .Select(row => TooltipText(grid, row, column))
            .ToList();
    }

    [Fact]
    public async Task A_property_signature_reads_as_IL_declares_the_property_with_its_index_parameters()
    {
        var properties = await GridAsync("Property");

        var count = TooltipText(properties, properties.RowNamed("Count"), "Signature");
        var indexer = TooltipText(properties, properties.RowNamed("Item"), "Signature");

        Assert.Equal("instance int32 Count()", count);
        Assert.Equal("instance !T Item(int32)", indexer);
    }

    [Fact]
    public async Task A_signature_of_locals_lists_each_local_by_its_index()
    {
        var signatures = ListTooltipTexts(await GridAsync("StandAloneSig"), "Signature");

        Assert.Contains(signatures, s => s is not null && s.StartsWith("[0] ", StringComparison.Ordinal));
        Assert.DoesNotContain(signatures, s => s is not null && s.Contains("/* signature", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_constant_reads_as_the_literal_it_holds()
    {
        var values = ListTooltipTexts(await GridAsync("Constant"), "Value");

        Assert.Contains("42", values);
    }
}
