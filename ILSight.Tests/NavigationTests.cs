using System.Reflection.Metadata.Ecma335;
using ILSight.Core;
using ILSight.Core.DiagramArea;
using ILSight.Core.MetadataArea;

namespace ILSight.Tests;

public sealed class NavigationTests
{
    private static MetadataTableInfo TableOf(BrowseNode node)
    {
        return UnionAsserts.GetCase<NodeSubject.MetadataTable>(node.Subject).Value;
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
        var calculator = await assembly.CalculatorAsync();
        var document = await calculator.DecompileAsync(DecompilationLanguage.CSharp);
        var line = document.Lines[document.LineContaining("private readonly List<T> _items")];

        var node = await list.FindNodeAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(line.SpanAt("List").Reference));

        Assert.NotNull(node);
        Assert.Equal("List<T>", node.Text);
        Assert.Equal(TypeShape.Class, node.TypeShape);
        Assert.Equal("System.Private.CoreLib", node.Assembly.Name);
        Assert.Contains(node.Assembly, list.Assemblies);
        var root = await node.Assembly.ReadRootAsync();
        var generic = await root.GetChildAsync("System.Collections.Generic");
        var listNode = await generic.GetChildAsync("List<T>");
        Assert.Same(node, listNode);
    }

    [Fact]
    public async Task A_member_reference_lands_on_the_member_node()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var calculator = await assembly.CalculatorAsync();
        var document = await calculator.DecompileAsync(DecompilationLanguage.CSharp);
        var call = document.Lines[document.LineContaining("_items.Add(item);")];

        var addOnList = await list.FindNodeAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(call.SpanAt("Add").Reference));
        var field = await list.FindNodeAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(call.SpanAt("_items").Reference));

        Assert.NotNull(addOnList);
        Assert.Equal("Add(T) : void", addOnList.Text);
        Assert.Equal("List<T>", addOnList.Parent?.Text);
        var items = await calculator.GetChildAsync("_items : List<T>");
        Assert.Same(items, field);
    }

    [Fact]
    public async Task A_reference_shows_in_metadata_as_the_row_of_its_definition_in_the_assembly_that_defines_it()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var calculator = await assembly.CalculatorAsync();
        var document = await calculator.DecompileAsync(DecompilationLanguage.CSharp);
        var call = document.Lines[document.LineContaining("_items.Add(item);")];

        var add = Assert.NotNull(await list.FindMetadataRowAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(call.SpanAt("Add").Reference)));
        var field = Assert.NotNull(await list.FindMetadataRowAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(call.SpanAt("_items").Reference)));
        var self = Assert.NotNull(await list.FindMetadataRowAsync(CodeReferences.CreateAssembly(assembly.Name, assembly.FilePath)));

        var methods = Assert.IsType<MetadataGrid.InMemory>(UnionAsserts.GetCase<Outcome<MetadataGrid>.Ok>(MetadataTables.EnsureGrid(TableOf(add.Node), NodeQueries.FirstGridRows)).Value);
        var fields = Assert.IsType<MetadataGrid.InMemory>(UnionAsserts.GetCase<Outcome<MetadataGrid>.Ok>(MetadataTables.EnsureGrid(TableOf(field.Node), NodeQueries.FirstGridRows)).Value);
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
        var calculator = await assembly.CalculatorAsync();
        var add = await calculator.GetChildAsync("Add(T) : void");
        var document = await add.DecompileAsync(DecompilationLanguage.CSharp);
        var item = document.SpansOf().First(s => s.Text == "item").Span.Reference;

        Assert.IsType<CodeReference.Local>(item);
    }

    [Fact]
    public async Task IL_type_references_resolve_through_the_referencing_assembly()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var calculator = await assembly.CalculatorAsync();
        var document = await calculator.DecompileAsync(DecompilationLanguage.IL);
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

        var add = await calculator.GetChildAsync("Add(T) : void");
        Assert.Contains("public void Add(T item)", csharp.Lines[Assert.NotNull(Documents.FindDefinitionLine(csharp, add))].Text);
        var count = await calculator.GetChildAsync("Count : int");
        Assert.Contains("public int Count", csharp.Lines[Assert.NotNull(Documents.FindDefinitionLine(csharp, count))].Text);
        var mode = await calculator.GetChildAsync("Mode");
        var fast = await mode.GetChildAsync("Fast : Mode");
        Assert.Contains("Fast", csharp.Lines[Assert.NotNull(Documents.FindDefinitionLine(csharp, fast))].Text);
        Assert.Contains("public class Calculator<T>", csharp.Lines[Assert.NotNull(Documents.FindDefinitionLine(csharp, calculator))].Text);
        var describe = await calculator.GetChildAsync("Describe() : string");
        Assert.Contains(".method", il.Lines[Assert.NotNull(Documents.FindDefinitionLine(il, describe))].Text);
        var fixtures = await assembly.FixturesAsync();
        var size = await fixtures.GetChildAsync("Size");
        Assert.Null(Documents.FindDefinitionLine(csharp, size));
        Assert.Null(Documents.FindDefinitionLine(csharp, fixtures));
    }

    [Fact]
    public async Task References_to_the_same_entity_are_equal_across_documents()
    {
        var calculator = await Trees.TestAssembly.CalculatorAsync();
        var typeDocument = await Trees.CalculatorDocumentAsync(DecompilationLanguage.CSharp);
        var describe = await calculator.GetChildAsync("Describe() : string");
        var memberDocument = await describe.DecompileAsync(DecompilationLanguage.CSharp);

        var countNode = await calculator.GetChildAsync("Count : int");
        var count = countNode.Definition;
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
        var calculator = await assembly.CalculatorAsync();
        var add = await calculator.GetChildAsync("Add(T) : void");
        var document = await add.DecompileAsync(DecompilationLanguage.CSharp);

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
        var coreLibRoot = await coreLib.ReadRootAsync();
        var generic = await coreLibRoot.GetChildAsync("System.Collections.Generic");
        var lambdaOwner = await generic.GetChildAsync("List<T>");
        var count = await lambdaOwner.GetChildAsync("Count : int");
        var document = await count.DecompileAsync(DecompilationLanguage.IL);
        var getter = document.SpansOf()
            .Select(static s => s.Span)
            .Where(static s => s.IsDefinition)
            .Select(static s => s.Reference)
            .OfType<CodeReference.Entity>()
            .First(static r => r.Kind == EntityKind.Property);

        var node = await list.FindNodeAsync(getter);

        var countNode = await lambdaOwner.GetChildAsync("Count : int");
        Assert.Same(countNode, node);
    }

    [Fact]
    public async Task IL_uses_through_member_references_match_their_definitions()
    {
        var calculator = await Trees.TestAssembly.CalculatorAsync();
        var document = await Trees.CalculatorDocumentAsync(DecompilationLanguage.IL);
        var addNode = await calculator.GetChildAsync("Add(T) : void");
        var add = addNode.Definition;
        var itemsNode = await calculator.GetChildAsync("_items : List<T>");
        var items = itemsNode.Definition;

        var uses = document.SpansOf().Where(s => s.Span is { IsDefinition: false, Reference: not null }).Select(s => s.Span.Reference).ToArray();

        Assert.Contains(add, uses);
        Assert.Contains(items, uses);
        Assert.Contains(".method", document.Lines[Assert.NotNull(Documents.FindDefinitionLine(document, add))].Text);
    }

    [Fact]
    public async Task Lookups_into_a_module_whose_types_cannot_be_read_name_it_rather_than_find_nothing()
    {
        using var temp = new TempDir();
        var unreadablePath = BrokenAssemblies.WriteUnreadableTypeSystem(temp, Trees.TestAssemblyPath);
        var path = AssemblyPath.Of(unreadablePath);
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var session = TestSession.Shared;
        var type = CodeReferences.CreateEntity(EntityKind.Type, "Circle", path, MetadataTokens.TypeDefinitionHandle(2));
        var (_, healthy) = Trees.OpenTestAssembly();
        var fixtures = await healthy.FixturesAsync();
        var place = new NodePath { FilePath = path, Steps = fixtures.Path.Steps };

        var node = (await NodeLookup.FindAsync(list, session, type, CancellationToken.None)).GetValue();
        var row = (await NodeLookup.FindMetadataRowAsync(list, session, type, CancellationToken.None)).GetValue();
        var atPlace = (await NodeLookup.FindAsync(list, session, place, CancellationToken.None)).GetValue();
        var tooltip = (await AssemblyListing.DescribeReferenceAsync(list, type, CancellationToken.None)).GetValue();
        var card = (await TypeCards.ReadAsync(list, type, CancellationToken.None)).GetValue();
        var module = Assert.IsType<LoadedModule>(Assert.Single(list.Assemblies));
        var byId = (await NodeLookup.FindByIdAsync(session, "T:ILSight.Tests.Fixtures.Circle", [module], CancellationToken.None)).GetValue();

        Assert.Equal("UnreadableTypes.dll", UnionAsserts.GetCase<Lookup<BrowseNode>.Unreadable>(node).Module.Module.FilePath.FileName);
        Assert.NotEmpty(UnionAsserts.GetCase<Lookup<BrowseNode>.Unreadable>(node).Module.Why.Message);
        Assert.Same(module, UnionAsserts.GetCase<Lookup<MetadataTarget>.Unreadable>(row).Module.Module);
        Assert.Same(module, UnionAsserts.GetCase<Lookup<BrowseNode>.Unreadable>(atPlace).Module.Module);
        Assert.Same(module, UnionAsserts.GetCase<Lookup<CodeTooltip>.Unreadable>(tooltip).Module.Module);
        Assert.Same(module, UnionAsserts.GetCase<Lookup<TypeCard>.Unreadable>(card).Module.Module);
        Assert.Same(module, UnionAsserts.GetCase<Lookup<BrowseNode>.Unreadable>(byId).Module.Module);
    }

    [Fact]
    public async Task A_documentation_id_among_members_the_library_cannot_read_names_the_module_unreadable_saying_why()
    {
        using var temp = new TempDir();
        var grow = BrokenAssemblies.GetMethod(Trees.TestAssemblyPath, "Circle", "Grow");
        var path = BrokenAssemblies.WriteUnreadableMethodName(temp, Trees.TestAssemblyPath, grow);
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var opened = AssemblyListing.Open(list, AssemblyPath.Of(path));
        var module = Assert.IsType<LoadedModule>(opened);

        var byId = await NodeLookup.FindByIdAsync(TestSession.Shared, "M:ILSight.Tests.Fixtures.Circle.Grow(System.Double,System.Int32)", [module],
            CancellationToken.None);

        // The library's failure on the user's module, read under the step's own catch: the module's, said as unreadable.
        var unreadable = UnionAsserts.GetCase<Lookup<BrowseNode>.Unreadable>(byId.GetValue());
        Assert.Same(module, unreadable.Module.Module);
        Assert.IsType<BadImageFormatException>(unreadable.Module.Why);
    }

    [Fact]
    public async Task A_bug_in_the_apps_work_on_a_paths_way_goes_on_to_the_caller_never_the_lookups_failure()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var fixtures = await assembly.FixturesAsync();
        var place = fixtures.Path;
        var session = new BrowseSession();
        var root = await BrowseSessions.EnsureRootAsync(session, assembly);
        BrokenListing.Settle(session, root);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => NodeLookup.FindAsync(list, session, place, CancellationToken.None));
        Assert.Equal(BrokenListing.Message, thrown.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => NodeLookup.FindListedAsync(list, session, place, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => NodeLookup.FindToExpandAsync(list, session, place, CancellationToken.None));
    }

    [Fact]
    public async Task A_member_whose_place_fails_to_list_is_the_lookups_failure_by_reference_id_and_path_never_an_ancestor_nor_not_found()
    {
        using var temp = new TempDir();
        var grow = BrokenAssemblies.GetMethod(Trees.TestAssemblyPath, "Circle", "Grow");
        var unreadablePath = BrokenAssemblies.WriteUnreadableMethodName(temp, Trees.TestAssemblyPath, grow);
        var path = AssemblyPath.Of(unreadablePath);
        var area = BrokenAssemblies.GetProperty(Trees.TestAssemblyPath, "Circle", "Area");
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var session = TestSession.Shared;
        var reference = CodeReferences.CreateEntity(EntityKind.Property, "Area", path, area);
        var (_, healthy) = Trees.OpenTestAssembly();
        var healthyFixtures = await healthy.FixturesAsync();
        var areaNode = await healthyFixtures.GetChildAsync("Circle", "Area : double");
        var place = new NodePath { FilePath = path, Steps = areaNode.Path.Steps };

        var byReference = await NodeLookup.FindAsync(list, session, reference, CancellationToken.None);
        var module = Assert.IsType<LoadedModule>(Assert.Single(list.Assemblies));
        var byId = await NodeLookup.FindByIdAsync(session, "P:ILSight.Tests.Fixtures.Circle.Area", [module], CancellationToken.None);
        var byPath = await NodeLookup.FindAsync(list, session, place, CancellationToken.None);
        var listedByPath = await NodeLookup.FindListedAsync(list, session, place, CancellationToken.None);
        var toExpand = await NodeLookup.FindToExpandAsync(list, session, place, CancellationToken.None);

        // The method whose name can't be read fails the listing of its namespace's types, Circle among them.
        var fixtures = await module.FixturesAsync();
        var childrenState = await BrowseSessions.GetChildrenAsync(session, fixtures);
        var why = UnionAsserts.GetCase<ChildrenState.Failed>(childrenState).Error;
        Assert.Same(why, UnionAsserts.GetCase<Outcome<Lookup<BrowseNode>>.Failed>(byReference).Error);
        Assert.Same(why, UnionAsserts.GetCase<Outcome<Lookup<BrowseNode>>.Failed>(byId).Error);
        Assert.Same(why, UnionAsserts.GetCase<Outcome<Lookup<BrowseNode>>.Failed>(byPath).Error);
        Assert.Same(why, UnionAsserts.GetCase<Outcome<Lookup<BrowseNode>>.Failed>(listedByPath).Error);
        UnionAsserts.GetCase<Lookup<BrowseNode>.NotFound>(toExpand.GetValue());
    }
}
