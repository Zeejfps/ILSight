using System.Reflection.Metadata.Ecma335;
using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.Core.Metadata;

namespace ILSight.Tests;

public sealed class NavigationTests
{
    private static MetadataTableInfo TableOf(BrowseNode node)
    {
        return Assert.IsType<NodeSubject.MetadataTable>(node.Subject).Value;
    }

    [Theory]
    [InlineData("N:ILSight.Tests.Fixtures", "ILSight.Tests.Fixtures")]
    [InlineData("T:ILSight.Tests.Fixtures.Circle", "Circle")]
    [InlineData("M:ILSight.Tests.Fixtures.Circle.Grow(System.Double,System.Int32)", "Grow(double, int) : double")]
    [InlineData("P:ILSight.Tests.Fixtures.Circle.Radius", "Radius : double")]
    public async Task A_documentation_id_finds_its_namespace_type_or_member(string id, string text)
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var node = await list.FindNodeByIdAsync(id, [assembly]);

        Assert.Equal(text, node?.Text);
    }

    [Theory]
    [InlineData("T:ILSight.Tests.Fixtures.Square")]
    [InlineData("N:Nowhere")]
    [InlineData("Circle")]
    [InlineData("M:ILSight.Tests.Fixtures.Circle.Grow(")]
    public async Task A_documentation_id_no_assembly_has_or_none_can_read_finds_nothing(string id)
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        Assert.Null(await list.FindNodeByIdAsync(id, [assembly]));
    }

    [Fact]
    public async Task A_documentation_id_of_a_referenced_type_is_not_found_in_the_assembly_that_only_uses_it()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        Assert.Null(await list.FindNodeByIdAsync("T:System.String", [assembly]));
    }

    [Fact]
    public async Task A_reference_into_the_framework_opens_the_defining_assembly()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var document = await assembly.CalculatorAsync().DecompileAsync(DecompilationLanguage.CSharp);
        var line = document.Lines[document.LineContaining("private readonly List<T> _items")];

        var node = await list.FindNodeAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(line.SpanAt("List").Reference));

        Assert.NotNull(node);
        Assert.Equal("List<T>", node.Text);
        Assert.Equal(TypeShape.Class, node.TypeShape);
        Assert.Equal("System.Private.CoreLib", node.Assembly.Name);
        Assert.Contains(node.Assembly, list.Assemblies);
        Assert.Same(node, await node.Assembly.ReadRootAsync().GetChildAsync("System.Collections.Generic").GetChildAsync("List<T>"));
    }

    [Fact]
    public async Task A_member_reference_lands_on_the_member_node()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var document = await assembly.CalculatorAsync().DecompileAsync(DecompilationLanguage.CSharp);
        var call = document.Lines[document.LineContaining("_items.Add(item);")];

        var addOnList = await list.FindNodeAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(call.SpanAt("Add").Reference));
        var field = await list.FindNodeAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(call.SpanAt("_items").Reference));

        Assert.NotNull(addOnList);
        Assert.Equal("Add(T) : void", addOnList.Text);
        Assert.Equal("List<T>", addOnList.Parent?.Text);
        Assert.Same(await assembly.CalculatorAsync().GetChildAsync("_items : List<T>"), field);
    }

    [Fact]
    public async Task A_reference_shows_in_metadata_as_the_row_of_its_definition_in_the_assembly_that_defines_it()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var document = await assembly.CalculatorAsync().DecompileAsync(DecompilationLanguage.CSharp);
        var call = document.Lines[document.LineContaining("_items.Add(item);")];

        var add = Assert.NotNull(await list.FindMetadataRowAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(call.SpanAt("Add").Reference)));
        var field = Assert.NotNull(await list.FindMetadataRowAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(call.SpanAt("_items").Reference)));
        var self = Assert.NotNull(await list.FindMetadataRowAsync(CodeReferences.CreateAssembly(assembly.Name, assembly.FilePath)));

        var methods = Assert.IsType<MetadataGrid.InMemory>(MetadataTables.EnsureGrid(TableOf(add.Node)));
        var fields = Assert.IsType<MetadataGrid.InMemory>(MetadataTables.EnsureGrid(TableOf(field.Node)));
        Assert.Equal("System.Private.CoreLib", add.Node.Assembly.Name);
        Assert.Equal(TableIndex.MethodDef, TableOf(add.Node).ListedTable);
        Assert.Equal("Add", MetadataGrids.GetText(methods, add.Row, methods.Column("Name")));
        Assert.Same(assembly, field.Node.Assembly);
        Assert.Equal("_items", MetadataGrids.GetText(fields, field.Row, fields.Column("Name")));
        Assert.Equal(TableIndex.Assembly, TableOf(self.Node).ListedTable);
        Assert.Equal(0, self.Row);
    }

    [Fact]
    public async Task A_local_has_no_metadata_row()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var document = await assembly.CalculatorAsync().GetChildAsync("Add(T) : void").DecompileAsync(DecompilationLanguage.CSharp);
        var item = document.SpansOf().First(s => s.Text == "item").Span.Reference;

        Assert.IsType<CodeReference.Local>(item);
    }

    [Fact]
    public async Task IL_type_references_resolve_through_the_referencing_assembly()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var document = await assembly.CalculatorAsync().DecompileAsync(DecompilationLanguage.IL);
        var objectReference = document.SpansOf().First(s => s.Text == "System.Object" && s.Span.Reference is not null).Span.Reference;
        Assert.NotNull(objectReference);

        var node = await list.FindNodeAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(objectReference));

        Assert.NotNull(node);
        Assert.Equal("Object", node.Text);
        Assert.Equal("System.Private.CoreLib", node.Assembly.Name);
    }

    [Fact]
    public async Task A_node_finds_its_definition_line_in_its_parents_document()
    {
        var assembly = Trees.TestAssembly;
        var calculator = await assembly.CalculatorAsync();

        var csharp = await Trees.CalculatorDocumentAsync(DecompilationLanguage.CSharp);
        var il = await Trees.CalculatorDocumentAsync(DecompilationLanguage.IL);

        Assert.Contains("public void Add(T item)", csharp.Lines[Assert.NotNull(Documents.FindDefinitionLine(csharp, await calculator.GetChildAsync("Add(T) : void")))].Text);
        Assert.Contains("public int Count", csharp.Lines[Assert.NotNull(Documents.FindDefinitionLine(csharp, await calculator.GetChildAsync("Count : int")))].Text);
        Assert.Contains("Fast", csharp.Lines[Assert.NotNull(Documents.FindDefinitionLine(csharp, await calculator.GetChildAsync("Mode").GetChildAsync("Fast : Mode")))].Text);
        Assert.Contains("public class Calculator<T>", csharp.Lines[Assert.NotNull(Documents.FindDefinitionLine(csharp, calculator))].Text);
        Assert.Contains(".method", il.Lines[Assert.NotNull(Documents.FindDefinitionLine(il, await calculator.GetChildAsync("Describe() : string")))].Text);
        Assert.Null(Documents.FindDefinitionLine(csharp, await assembly.FixturesAsync().GetChildAsync("Size")));
        Assert.Null(Documents.FindDefinitionLine(csharp, await assembly.FixturesAsync()));
    }

    [Fact]
    public async Task References_to_the_same_entity_are_equal_across_documents()
    {
        var calculator = await Trees.TestAssembly.CalculatorAsync();
        var typeDocument = await Trees.CalculatorDocumentAsync(DecompilationLanguage.CSharp);
        var memberDocument = await calculator.GetChildAsync("Describe() : string").DecompileAsync(DecompilationLanguage.CSharp);

        var count = (await calculator.GetChildAsync("Count : int")).Definition;
        var countInType = typeDocument.SpansOf().Where(s => s.Text == "Count").Select(s => s.Span.Reference).ToArray();
        var countInMember = memberDocument.SpansOf().First(s => s.Text == "Count").Span.Reference;

        Assert.Equal(2, countInType.Count(r => r == count));
        Assert.Equal(3, countInType.Count(r => r != count && r is CodeReference.Entity entity && entity.Kind == EntityKind.Property));
        Assert.Equal(count, countInMember);
    }

    [Fact]
    public async Task Locals_match_each_other_but_are_not_navigable()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var document = await assembly.CalculatorAsync().GetChildAsync("Add(T) : void").DecompileAsync(DecompilationLanguage.CSharp);

        var items = document.SpansOf().Where(s => s.Text == "item").Select(s => s.Span).ToArray();

        Assert.Equal(2, items.Length);
        Assert.True(items[0].IsDefinition);
        Assert.Equal(items[0].Reference, items[1].Reference);
        Assert.IsType<CodeReference.Local>(items[0].Reference);
    }

    [Fact]
    public async Task Hidden_compiler_generated_entities_fall_back_to_the_nearest_visible_ancestor()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var coreLib = list.Open(Trees.CoreLibPath);
        var lambdaOwner = await coreLib.ReadRootAsync().GetChildAsync("System.Collections.Generic").GetChildAsync("List<T>");
        var document = await lambdaOwner.GetChildAsync("Count : int").DecompileAsync(DecompilationLanguage.IL);
        var getter = document.SpansOf()
            .Select(static s => s.Span)
            .Where(static s => s.IsDefinition)
            .Select(static s => s.Reference)
            .OfType<CodeReference.Entity>()
            .First(static r => r.Kind == EntityKind.Property);

        var node = await list.FindNodeAsync(getter);

        Assert.Same(await lambdaOwner.GetChildAsync("Count : int"), node);
    }

    [Fact]
    public async Task IL_uses_through_member_references_match_their_definitions()
    {
        var calculator = await Trees.TestAssembly.CalculatorAsync();
        var document = await Trees.CalculatorDocumentAsync(DecompilationLanguage.IL);
        var add = (await calculator.GetChildAsync("Add(T) : void")).Definition;
        var items = (await calculator.GetChildAsync("_items : List<T>")).Definition;

        var uses = document.SpansOf().Where(s => s.Span is { IsDefinition: false, Reference: not null }).Select(s => s.Span.Reference).ToArray();

        Assert.Contains(add, uses);
        Assert.Contains(items, uses);
        Assert.Contains(".method", document.Lines[Assert.NotNull(Documents.FindDefinitionLine(document, add))].Text);
    }

    [Fact]
    public async Task Lookups_into_a_module_whose_types_cannot_be_read_name_it_rather_than_find_nothing()
    {
        using var temp = new TempDir();
        var path = AssemblyPath.Of(BrokenAssemblies.WriteUnreadableTypeSystem(temp, Trees.TestAssemblyPath));
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var session = TestSession.Shared;
        var type = CodeReferences.CreateEntity(EntityKind.Type, "Circle", path, MetadataTokens.TypeDefinitionHandle(2));
        var (_, healthy) = Trees.OpenTestAssembly();
        var place = new NodePath { FilePath = path, Steps = (await healthy.FixturesAsync()).Path.Steps };

        var node = await NodeLookup.FindAsync(list, session, type, CancellationToken.None);
        var row = await NodeLookup.FindMetadataRowAsync(list, session, type, CancellationToken.None);
        var atPlace = await NodeLookup.FindAsync(list, session, place, CancellationToken.None);
        var tooltip = await AssemblyListing.DescribeReferenceAsync(list, type, CancellationToken.None);
        var card = await TypeCards.ReadAsync(list, type, CancellationToken.None);
        var module = Assert.IsType<LoadedModule>(Assert.Single(list.Assemblies));
        var byId = await NodeLookup.FindByIdAsync(session, "T:ILSight.Tests.Fixtures.Circle", [module], CancellationToken.None);

        Assert.Equal("UnreadableTypes.dll", Assert.IsType<Lookup<BrowseNode>.Unreadable>(node).Module.Module.FilePath.FileName);
        Assert.NotEmpty(Assert.IsType<Lookup<BrowseNode>.Unreadable>(node).Module.Why.Message);
        Assert.Same(module, Assert.IsType<Lookup<MetadataTarget>.Unreadable>(row).Module.Module);
        Assert.Same(module, Assert.IsType<Lookup<BrowseNode>.Unreadable>(atPlace).Module.Module);
        Assert.Same(module, Assert.IsType<Lookup<CodeTooltip>.Unreadable>(tooltip).Module.Module);
        Assert.Same(module, Assert.IsType<Lookup<TypeCard>.Unreadable>(card).Module.Module);
        Assert.Same(module, Assert.IsType<Lookup<BrowseNode>.Unreadable>(byId).Module.Module);
    }
}
