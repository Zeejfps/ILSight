using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core;
using System.Text.RegularExpressions;
using ILSight.Core.Diagrams;

namespace ILSight.Tests;

public sealed class DiagramTests : IDisposable
{
    private const string FixturesNamespace = "ILSight.Tests.Fixtures";

    private static readonly SharedRead<DiagramModel> s_model = new(BuildModelAsync);

    private static async Task<DiagramModel> BuildModelAsync()
    {
        var main = await MainOfAsync(Trees.TestAssembly);
        return ClassDiagrams.BuildModel(Trees.TestAssembly, main, null, CancellationToken.None);
    }

    private static async Task<MetadataModule> MainOfAsync(LoadedModule module)
    {
        var read = await LoadedAssemblies.ReadTypeSystemAheadAsync(module, CancellationToken.None);
        return Assert.IsType<TypeSystemRead.Read>(read).TypeSystem.MainModule;
    }

    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    private static async Task<Dictionary<string, DiagramType>> FixturesAsync()
    {
        var model = await s_model.ReadAsync();
        return model.TypesByNamespace[FixturesNamespace];
    }

    private static async Task<DiagramType> TypeNamedAsync(string id)
    {
        return (await FixturesAsync())[id];
    }

    [Fact]
    public async Task The_diagrammer_is_written_as_a_page_with_its_script_styles_and_icon()
    {
        var folder = _temp.PathOf("diagram");

        var page = Assert.IsType<TypeSystemWork<string>.Done>(await ClassDiagrams.WriteAsync(Trees.TestAssembly, folder)).Value;

        Assert.Equal(Path.Combine(folder, ClassDiagrams.PageFileName), page);
        Assert.True(File.Exists(Path.Combine(folder, "script.js")));
        Assert.True(File.Exists(Path.Combine(folder, "styles.css")));
        Assert.True(File.Exists(Path.Combine(folder, "ILSpy.ico")));
        var html = await File.ReadAllTextAsync(page);
        Assert.DoesNotMatch(new Regex(@"\{\{\w+\}\}"), html);
        Assert.Contains("<title>ILSight.Tests class diagrammer - ILSpy</title>", html);
        Assert.Contains("\"TypesByNamespace\":{", html);
    }

    [Fact]
    public async Task A_generic_class_is_drawn_with_its_members_and_named_with_its_type_parameters()
    {
        var calculator = await TypeNamedAsync("Calculator");

        Assert.Equal("Calculator❰T❱", calculator.Name);
        Assert.StartsWith("class Calculator {", calculator.Body);
        Assert.Contains("+int Count", calculator.Body);
        Assert.Contains("+Add(T item) void", calculator.Body);
        Assert.Contains("~Describe() string", calculator.Body);
        Assert.Contains("+int Answer$", calculator.Body);
        Assert.DoesNotContain("op_Addition", calculator.Body);
        Assert.DoesNotContain("<Count>k__BackingField", calculator.Body);
    }

    [Fact]
    public async Task An_enum_lists_its_values_and_an_interface_is_annotated()
    {
        var mode = (await FixturesAsync()).Values.Single(t => t.Body.Contains("Precise"));
        var shape = await TypeNamedAsync("IShape");

        Assert.Contains("<<Enumeration>>\n    Fast\n    Slow\n    Precise", mode.Body);
        Assert.Contains("<<Interface>>", shape.Body);
        Assert.Contains("+double Area*", shape.Body);
    }

    [Fact]
    public async Task The_xml_documentation_of_types_and_members_is_carried_as_plain_text()
    {
        var calculator = await TypeNamedAsync("Calculator");

        Assert.NotNull(calculator.XmlDocs);
        Assert.Equal("How many items were added.", calculator.XmlDocs["Count"]);
        Assert.StartsWith("Adds an item and raises [ILSight.Tests.Fixtures.Calculator`1.Changed].", calculator.XmlDocs["Add"]);
        Assert.Equal("The space the shape covers.", (await TypeNamedAsync("IShape")).XmlDocs?["Area"]);
    }

    [Fact]
    public async Task Types_outside_the_assembly_that_its_types_derive_from_are_named_as_outside_references()
    {
        var references = (await s_model.ReadAsync()).OutsideReferences;

        Assert.Equal("System.IDisposable", references["System_IDisposable"]);
        Assert.Equal("System.IProgress❰T❱", references["System_IProgress_T"]);
    }

    [Fact]
    public async Task Building_stops_when_cancelled()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ClassDiagrams.WriteAsync(Trees.TestAssembly, _temp.PathOf("cancelled"), null, cancel.Token));
        Assert.False(Directory.Exists(_temp.PathOf("cancelled")));
    }

    [Fact]
    public async Task Progress_counts_up_to_every_type()
    {
        var progress = new RecordedProgress();

        ClassDiagrams.BuildModel(Trees.TestAssembly, await MainOfAsync(Trees.TestAssembly), progress, CancellationToken.None);

        var reports = progress.Reports;
        Assert.Equal(1f, reports[^1].Fraction);
        Assert.True(reports.Count > 1);
    }
}
