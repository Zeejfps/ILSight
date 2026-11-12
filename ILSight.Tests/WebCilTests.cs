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
    public async Task A_webcil_modules_grid_is_read_whole_as_its_content_is_made()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.WebCilSamplePath);

        var tables = await assembly.ReadRootAsync().GetChildAsync("Metadata", "Tables");
        var grid = Assert.IsType<NodeContent.Table>(await tables.TableAsync("TypeDef").ReadContentAsync()).Grid;

        var whole = Assert.IsType<MetadataGrid.ReadWhole>(grid);
        var counter = grid.RowNamed("Counter");
        Assert.Equal(grid.RowCount * grid.Columns.Count, whole.Values.Count);
        Assert.Equal("Counter", whole.Values[counter * grid.Columns.Count + grid.Column("Name")]);
        Assert.NotNull(MetadataGrids.FindTooltip(grid, counter, grid.Column("Attributes")));
        Assert.NotNull(MetadataGrids.FindLink(grid, counter, grid.Column("BaseType")));
        Assert.NotNull(MetadataGrids.FindDefinition(grid, counter));
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
