using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.Tests;

public sealed class WebCilTests
{
    /// <summary>The magic and version every WebAssembly module starts with, followed here by nothing.</summary>
    private static readonly byte[] WebAssemblyHeader = [0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00];

    [Fact]
    public async Task A_webcil_module_opens_as_an_assembly_and_decompiles()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.WebCilSamplePath);

        var counter = await assembly.ReadRootAsync().GetChildAsync("SampleApp").GetChildAsync("Counter").DecompileAsync(DecompilationLanguage.CSharp);

        Assert.IsNotType<FailedAssembly>(assembly);
        Assert.IsNotType<LoadedPackage>(assembly);
        Assert.Equal("SampleApp", assembly.Name);
        Assert.Null(counter.Error);
        Assert.Contains("public void Increment()", Documents.GetText(counter));
    }

    [Fact]
    public async Task A_webcil_module_shows_its_metadata_without_pe_headers()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.WebCilSamplePath);

        var metadata = await assembly.ReadRootAsync().GetChildAsync("Metadata").ListChildrenAsync();

        Assert.DoesNotContain(metadata, static c => c.Is(MetadataGridRole.Header));
        Assert.Contains(metadata, static c => c.Subject is NodeSubject.MetadataTables);
    }

    [Fact]
    public async Task A_webcil_modules_grid_is_paged_its_first_page_read_with_its_content()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.WebCilSamplePath);

        var tables = await assembly.ReadRootAsync().GetChildAsync("Metadata", "Tables");
        var grid = Assert.IsType<NodeContent.Table>(await tables.TableAsync("TypeDef").ReadContentAsync()).Grid;

        Assert.IsType<MetadataGrid.Paged>(grid);
        var counter = grid.RowNamed("Counter");
        Assert.True(grid.IsRowRead(grid.RowCount - 1));
        Assert.NotNull(MetadataGrids.FindTooltip(grid, counter, grid.Column("Attributes")));
        Assert.NotNull(MetadataGrids.FindLink(grid, counter, grid.Column("BaseType")));
        Assert.NotNull(MetadataGrids.FindDefinition(grid, counter));
    }

    [Fact]
    public async Task A_webcil_modules_rows_past_the_first_page_show_once_their_page_is_read()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.WebCilSamplePath);
        var strings = await assembly.ReadRootAsync().GetChildAsync("Metadata", "String Heap (100)");
        var grid = Assert.IsType<MetadataGrid.Paged>(Assert.IsType<NodeContent.Table>(await strings.ReadContentAsync()).Grid);
        var last = grid.RowCount - 1;
        var value = grid.Column("Offset");

        var unread = (grid.IsRowRead(last), MetadataGrids.GetText(grid, last, value), MetadataGrids.FindTooltip(grid, last, value));
        var asked = (grid.Ask(last), grid.Ask(MetadataGrid.Paged.RowsPerPage));
        grid.AddPage(MetadataGrid.Paged.PageOf(last), MetadataGrids.ReadPage(grid, MetadataGrid.Paged.PageOf(last)));

        Assert.True(grid.IsRowRead(MetadataGrid.Paged.RowsPerPage - 1));
        Assert.Equal((false, "", null), unread);
        Assert.Equal((true, false), asked);
        Assert.True(grid.IsRowRead(last));
        Assert.NotEqual("", MetadataGrids.GetText(grid, last, value));
        Assert.False(grid.Ask(last));
    }

    [Fact]
    public async Task Sorting_a_webcil_modules_grid_reads_every_row_through_its_file()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.WebCilSamplePath);
        var strings = await assembly.ReadRootAsync().GetChildAsync("Metadata", "String Heap (100)");
        var grid = Assert.IsType<NodeContent.Table>(await strings.ReadContentAsync()).Grid;

        var order = MetadataGrids.OrderRows(grid, grid.Column("Value"), descending: false);

        Assert.Equal(Enumerable.Range(0, grid.RowCount), order.Order());
        Assert.False(grid.IsRowRead(grid.RowCount - 1));
    }

    [Fact]
    public void A_webcil_module_offers_no_pdb_to_generate()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.OpenModule(Trees.WebCilSamplePath);

        Assert.False(assembly.CanGeneratePdb);
    }

    [Fact]
    public async Task The_summary_of_a_webcil_module_leaves_out_the_architecture()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.WebCilSamplePath);

        var summary = Documents.GetText(await assembly.ReadRootAsync().DecompileAsync(DecompilationLanguage.CSharp));

        Assert.Contains(Trees.WebCilSamplePath, summary);
        Assert.DoesNotContain("Architecture:", summary);
    }

    [Fact]
    public void A_webassembly_module_that_holds_no_assembly_fails_to_load()
    {
        using var temp = new TempDir();
        var path = temp.Write("dotnet.native.wasm", WebAssemblyHeader);

        var failed = new AssemblyList { Runtime = TestRuntime.Installed }.Open(path);

        Assert.IsType<FailedAssembly>(failed);
    }
}
