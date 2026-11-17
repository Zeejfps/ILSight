using ILSight.Core;

namespace ILSight.Tests;

public sealed class BrowseTreeTests
{
    [Fact]
    public async Task An_assembly_lists_metadata_references_then_flat_sorted_namespaces()
    {
        var assembly = Trees.TestAssembly;
        var root = await assembly.ReadRootAsync();

        Assert.IsType<NodeSubject.Module>(root.Subject);
        // MinVer versions the build from the release tags, so the label names whatever this one is.
        var version = typeof(BrowseTreeTests).Assembly.GetName().Version;
        Assert.Equal($"ILSight.Tests ({version}, .NETCoreApp, v11.0)", root.Text);
        Assert.IsType<NodeSubject.Metadata>((await root.ListChildrenAsync())[0].Subject);
        var children = await root.ListChildrenAsync();
        Assert.IsType<NodeSubject.References>(children.First(c => c.Subject is not NodeSubject.Metadata).Subject);
        var namespaces = children.Where(c => c.Subject is NodeSubject.Namespace).Select(c => c.Text).ToArray();
        Assert.Equal(["-", "ILSight", "ILSight.Tests", "ILSight.Tests.Fixtures"], namespaces);
        var references = await root.GetChildAsync("References");
        var referencesChildren = await references.ListChildrenAsync();
        Assert.Contains(referencesChildren, r => r.Subject is NodeSubject.AssemblyReference && r.Text == "System.Runtime");
    }

    [Fact]
    public async Task Types_have_kinds_labels_and_accessibility()
    {
        var assembly = Trees.TestAssembly;
        var fixtures = await assembly.FixturesAsync();

        var children = await fixtures.ListChildrenAsync();
        Assert.Equal(
            [
                ("Animal", TypeShape.Class), ("Calculator<T>", TypeShape.Class), ("Callback", TypeShape.Delegate), ("Circle", TypeShape.Class),
                ("Enclosure", TypeShape.Struct), ("IFeeds", TypeShape.Interface), ("IShape", TypeShape.Interface), ("Keeper", TypeShape.Class),
                ("Lion", TypeShape.Class), ("NativeImports", TypeShape.Class), ("Parrot", TypeShape.Class), ("Point", TypeShape.Record),
                ("Size", TypeShape.Struct), ("Visits", TypeShape.Class),
            ],
            children.Select(c => (c.Text, c.TypeShape)));
        var circle = await fixtures.GetChildAsync("Circle");
        Assert.Equal(NodeAccessibility.Internal, circle.Accessibility);
        Assert.False(circle.IsPublicApi);
        var area = await circle.GetChildAsync("Area : double");
        Assert.False(area.IsPublicApi);
        Assert.Equal(NodeAccessibility.Public, area.Accessibility);
        var calculator = await fixtures.GetChildAsync("Calculator<T>");
        Assert.Equal("ILSight.Tests.Fixtures.Calculator<T>", calculator.FullName);
    }

    [Fact]
    public async Task Type_members_come_in_ILSpy_order_with_signatures()
    {
        var assembly = Trees.TestAssembly;

        var calculator = await assembly.CalculatorAsync();
        var calculatorChildren = await calculator.ListChildrenAsync();
        var children = calculatorChildren
            .Where(c => !c.IsCompilerGenerated)
            .ToArray();

        Assert.Equal(["Base Types", "Derived Types", "Mode"], children.Take(3).Select(static c => c.Text));
        Assert.IsType<NodeSubject.BaseTypes>(children[0].Subject);
        Assert.IsType<NodeSubject.DerivedTypes>(children[1].Subject);
        Assert.Equal(TypeShape.Enum, children[2].TypeShape);
        Assert.Equal(
            [
                (MemberShape.Constant, "Answer : int"),
                (MemberShape.Field, "_items : List<T>"),
                (MemberShape.Property, "Count : int"),
                (MemberShape.Indexer, "this[int] : T"),
                (MemberShape.Event, "Changed : EventHandler?"),
                (MemberShape.Constructor, "Calculator()"),
                (MemberShape.Method, "Add(T) : void"),
                (MemberShape.Method, "Describe() : string"),
                (MemberShape.Operator, "operator +(Calculator<T>, T) : Calculator<T>"),
                (MemberShape.Method, "TryGet(int, out T?) : bool"),
            ],
            children.Skip(3).Select(static c => (c.MemberShape, c.Text)));
    }

    [Fact]
    public async Task Enum_members_keep_declaration_order_without_the_value_field()
    {
        var assembly = Trees.TestAssembly;

        var calculator = await assembly.CalculatorAsync();
        var mode = await calculator.GetChildAsync("Mode");

        var children = await mode.ListChildrenAsync();
        Assert.Equal(["Base Types", "Fast : Mode", "Slow : Mode", "Precise : Mode"], children.Select(c => c.Text));
    }

    [Fact]
    public async Task Properties_and_events_hold_their_accessors()
    {
        var assembly = Trees.TestAssembly;
        var calculator = await assembly.CalculatorAsync();

        var count = await calculator.GetChildAsync("Count : int");
        var children = await count.ListChildrenAsync();
        Assert.Equal(["get_Count"], children.Select(c => c.Text));
        var changed = await calculator.GetChildAsync("Changed : EventHandler?");
        var changedAccessors = await changed.ListChildrenAsync();
        Assert.Equal(["add_Changed", "remove_Changed"], changedAccessors.Select(c => c.Text));
        var countChildren = await count.ListChildrenAsync();
        Assert.All(countChildren, c => Assert.Equal(MemberShape.Method, c.MemberShape));
    }

    [Fact]
    public async Task Derived_types_list_the_open_types_deriving_directly_and_point_at_them()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var fixtures = await assembly.FixturesAsync();

        var iShape = await fixtures.GetChildAsync("IShape");
        var derivedTypes = await iShape.GetChildAsync("Derived Types");
        var derived = await derivedTypes.ListChildrenAsync();

        var circle = Assert.Single(derived);
        Assert.IsType<NodeSubject.DerivedType>(circle.Subject);
        Assert.Equal("Circle", circle.Text);
        Assert.Equal(NodeAccessibility.Internal, circle.Accessibility);
        Assert.False(circle.HasChildren);
        var circleNode = await fixtures.GetChildAsync("Circle");
        Assert.Same(circleNode, await list.FindNodeAsync(circle.Destination));
        var circleChildren = await circleNode.ListChildrenAsync();
        Assert.DoesNotContain(circleChildren, c => c.Subject is NodeSubject.DerivedTypes);
        var size = await fixtures.GetChildAsync("Size");
        var sizeChildren = await size.ListChildrenAsync();
        Assert.DoesNotContain(sizeChildren, c => c.Subject is NodeSubject.DerivedTypes);
        var calculator = await assembly.CalculatorAsync();
        var derivedTypesNode = await calculator.GetChildAsync("Derived Types");
        var derivedTypesChildren = await derivedTypesNode.ListChildrenAsync();
        Assert.Empty(derivedTypesChildren);
        Assert.Equal("Circle", circle.Analysis?.Label);
    }

    [Fact]
    public async Task Derived_types_across_three_modules_the_middle_one_unreadable_list_the_others_types_and_a_row_naming_it()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var shapesPath = WrittenAssemblies.WriteWithBase(temp, "Shapes", "Shape", "Circle");
        var shapes = list.OpenModule(shapesPath);
        var billingPath = WrittenAssemblies.WriteDeriving(temp, "Billing", "Shapes", "Shape", "Invoice");
        var unreadableBillingPath = BrokenAssemblies.WriteUnreadableTypeSystem(temp, billingPath);
        list.OpenModule(unreadableBillingPath);
        var ordersPath = WrittenAssemblies.WriteDeriving(temp, "Orders", "Shapes", "Shape", "Order");
        list.OpenModule(ordersPath);
        var shapesRoot = await shapes.ReadRootAsync();
        var folder = await shapesRoot.GetChildAsync("Shapes", "Shape", "Derived Types");

        var derived = await folder.ListChildrenAsync();

        Assert.Equal(["Circle", "Order"], derived.Where(static c => c.Subject is NodeSubject.DerivedType).Select(static c => c.Text));
        var row = derived[^1];
        var unreadable = Assert.IsType<NodeSubject.UnreadableModule>(row.Subject).Value;
        Assert.Equal("UnreadableTypes.dll", unreadable.Module.FilePath.FileName);
        Assert.Equal($"UnreadableTypes.dll: couldn't be read ({unreadable.Why.Message})", row.Text);
        Assert.Equal(3, derived.Count);
        Assert.False(row.HasChildren);
        Assert.Same(row, await list.FindNodeAsync(row.Path));
        Assert.Contains(row.Text, Documents.GetText(await folder.DecompileAsync(DecompilationLanguage.CSharp)));
        Assert.Contains(row.Text, Documents.GetText(await row.DecompileAsync(DecompilationLanguage.CSharp)));
    }

    [Fact]
    public async Task A_derived_type_expands_to_the_types_deriving_from_it_in_turn()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var corelib = list.Open(Trees.CoreLibPath);
        var corelibRoot = await corelib.ReadRootAsync();
        var systemNode = await corelibRoot.GetChildAsync("System");
        var exception = await systemNode.GetChildAsync("Exception");

        var derivedTypes = await exception.GetChildAsync("Derived Types");
        var children = await derivedTypes.ListChildrenAsync();
        var system = children.Single(c => c.Text.EndsWith("SystemException", StringComparison.Ordinal));
        var systemDerived = await system.ListChildrenAsync();
        var argument = systemDerived.Single(c => c.Text.EndsWith("ArgumentException", StringComparison.Ordinal));

        Assert.IsType<NodeSubject.DerivedType>(argument.Subject);
        var argumentChildren = await argument.ListChildrenAsync();
        Assert.Contains(argumentChildren, c => c.Text.EndsWith("ArgumentNullException", StringComparison.Ordinal));
        Assert.Contains(system.Text, Documents.GetText(await derivedTypes.DecompileAsync(DecompilationLanguage.CSharp)));
    }

    [Fact]
    public async Task Base_types_point_at_their_definitions()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var fixtures = await assembly.FixturesAsync();
        var circle = await fixtures.GetChildAsync("Circle");
        var baseTypes = await circle.GetChildAsync("Base Types");
        var bases = await baseTypes.ListChildrenAsync();

        Assert.Equal(["object", "IShape"], bases.Select(b => b.Text));
        Assert.All(bases, b => Assert.IsType<NodeSubject.BaseType>(b.Subject));
        var iShape = await fixtures.GetChildAsync("IShape");
        Assert.Same(iShape, await list.FindNodeAsync(bases[1].Destination));
    }

    [Fact]
    public async Task HasChildren_answers_without_building_children_and_agrees_after()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var calculator = await assembly.CalculatorAsync();

        var add = await calculator.GetChildAsync("Add(T) : void");
        var fixtures = await assembly.FixturesAsync();
        var size = await fixtures.GetChildAsync("Size");

        Assert.False(add.HasChildren);
        Assert.True(size.HasChildren);
        var sizeChildren = await size.ListChildrenAsync();
        Assert.NotEmpty(sizeChildren);
        var count = await calculator.GetChildAsync("Count : int");
        Assert.True(count.HasChildren);
    }

    [Fact]
    public async Task Nodes_keep_their_identity_across_reads_and_threads()
    {
        var (_, assembly) = Trees.OpenTestAssembly();

        var nodes = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => GetAddAsync(assembly)))).WithinPatienceAsync();

        Assert.All(nodes, n => Assert.Same(nodes[0], n));
        var calculator = await assembly.CalculatorAsync();
        Assert.Same(calculator, nodes[0].Parent);
        Assert.Same(assembly, nodes[0].Assembly);
    }

    // Calculator's Add, its tree read as far as it.
    private static async Task<BrowseNode> GetAddAsync(LoadedAssembly assembly)
    {
        var calculator = await assembly.CalculatorAsync();
        return await calculator.GetChildAsync("Add(T) : void");
    }

    [Fact]
    public async Task A_node_found_by_its_entity_is_the_one_its_parent_lists_whichever_thread_builds_it_first()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var calculator = await Trees.TestAssembly.CalculatorAsync();
        var add = await calculator.GetChildAsync("Add(T) : void");
        var reference = add.Definition;

        var nodes = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(i => Task.Run(async () => i % 2 == 0 ? await list.FindNodeAsync(reference) : await GetAddAsync(assembly)))).WithinPatienceAsync();

        Assert.All(nodes, n => Assert.Same(nodes[0], n));
        var assemblyCalculator = await assembly.CalculatorAsync();
        Assert.Same(assemblyCalculator, Assert.IsAssignableFrom<BrowseNode>(nodes[0]).Parent);
    }

    [Fact]
    public async Task A_path_in_the_list_that_holds_its_node_finds_that_very_node()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var calculator = await assembly.CalculatorAsync();
        var add = await calculator.GetChildAsync("Add(T) : void");

        Assert.Same(add, await list.FindNodeAsync(add.Path));
        Assert.Same(add, await list.FindListedNodeAsync(add.Path));
        var root = await assembly.ReadRootAsync();
        Assert.Same(root, await list.FindNodeAsync(root.Path));
    }

    [Fact]
    public async Task Entity_nodes_carry_a_reference_to_themselves()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var calculator = await assembly.CalculatorAsync();
        var add = await calculator.GetChildAsync("Add(T) : void");

        Assert.Equal(EntityKind.Method, add.Definition.Kind);
        Assert.Same(add, await list.FindNodeAsync(add.Definition));
        var fixtures = await assembly.FixturesAsync();
        Assert.Null(fixtures.Reference);
    }

    [Fact]
    public async Task CoreLib_builds_its_tree()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var coreLib = list.Open(Trees.CoreLibPath);

        var coreLibRoot = await coreLib.ReadRootAsync();
        var generic = await coreLibRoot.GetChildAsync("System.Collections.Generic");
        var dictionary = await generic.GetChildAsync("Dictionary<TKey, TValue>");

        var dictionaryChildren = await dictionary.ListChildrenAsync();
        Assert.Contains(dictionaryChildren, c => c.Text == "TryGetValue(TKey, out TValue) : bool");
        Assert.Contains(dictionaryChildren, c => c.Is(TypeShape.Struct) && c.Text == "Enumerator");
        var coreLibRootChildren = await coreLibRoot.ListChildrenAsync();
        Assert.Contains(coreLibRootChildren, c => c.Subject is NodeSubject.Resources);
    }
}
