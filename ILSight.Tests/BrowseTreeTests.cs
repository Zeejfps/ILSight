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
        Assert.Equal($"ILSight.Tests ({version}, .NETCoreApp, v10.0)", root.Text);
        Assert.IsType<NodeSubject.Metadata>((await root.ListChildrenAsync())[0].Subject);
        Assert.IsType<NodeSubject.References>((await root.ListChildrenAsync()).First(c => c.Subject is not NodeSubject.Metadata).Subject);
        var namespaces = (await root.ListChildrenAsync()).Where(c => c.Subject is NodeSubject.Namespace).Select(c => c.Text).ToArray();
        Assert.Equal(["-", "ILSight", "ILSight.Tests", "ILSight.Tests.Fixtures"], namespaces);
        Assert.Contains(await root.GetChildAsync("References").ListChildrenAsync(), r => r.Subject is NodeSubject.AssemblyReference && r.Text == "System.Runtime");
    }

    [Fact]
    public async Task Types_have_kinds_labels_and_accessibility()
    {
        var assembly = Trees.TestAssembly;
        var fixtures = await assembly.FixturesAsync();

        Assert.Equal(
            [
                ("Animal", TypeShape.Class), ("Calculator<T>", TypeShape.Class), ("Callback", TypeShape.Delegate), ("Circle", TypeShape.Class),
                ("Enclosure", TypeShape.Struct), ("IFeeds", TypeShape.Interface), ("IShape", TypeShape.Interface), ("Keeper", TypeShape.Class),
                ("Lion", TypeShape.Class), ("NativeImports", TypeShape.Class), ("Parrot", TypeShape.Class), ("Point", TypeShape.Record),
                ("Size", TypeShape.Struct), ("Visits", TypeShape.Class),
            ],
            (await fixtures.ListChildrenAsync()).Select(c => (c.Text, c.TypeShape)));
        var circle = await fixtures.GetChildAsync("Circle");
        Assert.Equal(NodeAccessibility.Internal, circle.Accessibility);
        Assert.False(circle.IsPublicApi);
        Assert.False((await circle.GetChildAsync("Area : double")).IsPublicApi);
        Assert.Equal(NodeAccessibility.Public, (await circle.GetChildAsync("Area : double")).Accessibility);
        Assert.Equal("ILSight.Tests.Fixtures.Calculator<T>", (await fixtures.GetChildAsync("Calculator<T>")).FullName);
    }

    [Fact]
    public async Task Type_members_come_in_ILSpy_order_with_signatures()
    {
        var assembly = Trees.TestAssembly;

        var children = (await assembly.CalculatorAsync().ListChildrenAsync())
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

        var mode = await assembly.CalculatorAsync().GetChildAsync("Mode");

        Assert.Equal(["Base Types", "Fast : Mode", "Slow : Mode", "Precise : Mode"], (await mode.ListChildrenAsync()).Select(c => c.Text));
    }

    [Fact]
    public async Task Properties_and_events_hold_their_accessors()
    {
        var assembly = Trees.TestAssembly;
        var calculator = await assembly.CalculatorAsync();

        Assert.Equal(["get_Count"], (await calculator.GetChildAsync("Count : int").ListChildrenAsync()).Select(c => c.Text));
        Assert.Equal(["add_Changed", "remove_Changed"], (await calculator.GetChildAsync("Changed : EventHandler?").ListChildrenAsync()).Select(c => c.Text));
        Assert.All(await calculator.GetChildAsync("Count : int").ListChildrenAsync(), c => Assert.Equal(MemberShape.Method, c.MemberShape));
    }

    [Fact]
    public async Task Derived_types_list_the_open_types_deriving_directly_and_point_at_them()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var fixtures = await assembly.FixturesAsync();

        var derived = await fixtures.GetChildAsync("IShape").GetChildAsync("Derived Types").ListChildrenAsync();

        var circle = Assert.Single(derived);
        Assert.IsType<NodeSubject.DerivedType>(circle.Subject);
        Assert.Equal("Circle", circle.Text);
        Assert.Equal(NodeAccessibility.Internal, circle.Accessibility);
        Assert.False(circle.HasChildren);
        Assert.Same(await fixtures.GetChildAsync("Circle"), await list.FindNodeAsync(circle.Destination));
        Assert.DoesNotContain(await fixtures.GetChildAsync("Circle").ListChildrenAsync(), c => c.Subject is NodeSubject.DerivedTypes);
        Assert.DoesNotContain(await fixtures.GetChildAsync("Size").ListChildrenAsync(), c => c.Subject is NodeSubject.DerivedTypes);
        Assert.Empty(await assembly.CalculatorAsync().GetChildAsync("Derived Types").ListChildrenAsync());
        Assert.Equal("Circle", circle.Analysis?.Label);
    }

    [Fact]
    public async Task Derived_types_across_three_modules_the_middle_one_unreadable_list_the_others_types_and_a_row_naming_it()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var shapes = list.OpenModule(WrittenAssemblies.WriteWithBase(temp, "Shapes", "Shape", "Circle"));
        list.OpenModule(BrokenAssemblies.WriteUnreadableTypeSystem(temp, WrittenAssemblies.WriteDeriving(temp, "Billing", "Shapes", "Shape", "Invoice")));
        list.OpenModule(WrittenAssemblies.WriteDeriving(temp, "Orders", "Shapes", "Shape", "Order"));
        var folder = await shapes.ReadRootAsync().GetChildAsync("Shapes", "Shape", "Derived Types");

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
        var exception = await corelib.ReadRootAsync().GetChildAsync("System").GetChildAsync("Exception");

        var system = (await exception.GetChildAsync("Derived Types").ListChildrenAsync()).Single(c => c.Text.EndsWith("SystemException", StringComparison.Ordinal));
        var argument = (await system.ListChildrenAsync()).Single(c => c.Text.EndsWith("ArgumentException", StringComparison.Ordinal));

        Assert.IsType<NodeSubject.DerivedType>(argument.Subject);
        Assert.Contains(await argument.ListChildrenAsync(), c => c.Text.EndsWith("ArgumentNullException", StringComparison.Ordinal));
        Assert.Contains(system.Text, Documents.GetText(await exception.GetChildAsync("Derived Types").DecompileAsync(DecompilationLanguage.CSharp)));
    }

    [Fact]
    public async Task Base_types_point_at_their_definitions()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var bases = await assembly.FixturesAsync().GetChildAsync("Circle").GetChildAsync("Base Types").ListChildrenAsync();

        Assert.Equal(["object", "IShape"], bases.Select(b => b.Text));
        Assert.All(bases, b => Assert.IsType<NodeSubject.BaseType>(b.Subject));
        Assert.Same(await assembly.FixturesAsync().GetChildAsync("IShape"), await list.FindNodeAsync(bases[1].Destination));
    }

    [Fact]
    public async Task HasChildren_answers_without_building_children_and_agrees_after()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var calculator = await assembly.CalculatorAsync();

        var add = await calculator.GetChildAsync("Add(T) : void");
        var size = await assembly.FixturesAsync().GetChildAsync("Size");

        Assert.False(add.HasChildren);
        Assert.True(size.HasChildren);
        Assert.NotEmpty(await size.ListChildrenAsync());
        Assert.True((await calculator.GetChildAsync("Count : int")).HasChildren);
    }

    [Fact]
    public async Task Nodes_keep_their_identity_across_reads_and_threads()
    {
        var (_, assembly) = Trees.OpenTestAssembly();

        var nodes = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => assembly.CalculatorAsync().GetChildAsync("Add(T) : void"))));

        Assert.All(nodes, n => Assert.Same(nodes[0], n));
        Assert.Same(await assembly.CalculatorAsync(), nodes[0].Parent);
        Assert.Same(assembly, nodes[0].Assembly);
    }

    [Fact]
    public async Task A_node_found_by_its_entity_is_the_one_its_parent_lists_whichever_thread_builds_it_first()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var reference = (await Trees.TestAssembly.CalculatorAsync().GetChildAsync("Add(T) : void")).Definition;

        var nodes = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(i => Task.Run(async () => i % 2 == 0 ? await list.FindNodeAsync(reference) : await assembly.CalculatorAsync().GetChildAsync("Add(T) : void"))));

        Assert.All(nodes, n => Assert.Same(nodes[0], n));
        Assert.Same(await assembly.CalculatorAsync(), Assert.IsAssignableFrom<BrowseNode>(nodes[0]).Parent);
    }

    [Fact]
    public async Task A_path_in_the_list_that_holds_its_node_finds_that_very_node()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var add = await assembly.CalculatorAsync().GetChildAsync("Add(T) : void");

        Assert.Same(add, await list.FindNodeAsync(add.Path));
        Assert.Same(add, await list.FindListedNodeAsync(add.Path));
        Assert.Same(await assembly.ReadRootAsync(), await list.FindNodeAsync((await assembly.ReadRootAsync()).Path));
    }

    [Fact]
    public async Task Entity_nodes_carry_a_reference_to_themselves()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var add = await assembly.CalculatorAsync().GetChildAsync("Add(T) : void");

        Assert.Equal(EntityKind.Method, add.Definition.Kind);
        Assert.Same(add, await list.FindNodeAsync(add.Definition));
        Assert.Null((await assembly.FixturesAsync()).Reference);
    }

    [Fact]
    public async Task CoreLib_builds_its_tree()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var coreLib = list.Open(Trees.CoreLibPath);

        var generic = await coreLib.ReadRootAsync().GetChildAsync("System.Collections.Generic");
        var dictionary = await generic.GetChildAsync("Dictionary<TKey, TValue>");

        Assert.Contains(await dictionary.ListChildrenAsync(), c => c.Text == "TryGetValue(TKey, out TValue) : bool");
        Assert.Contains(await dictionary.ListChildrenAsync(), c => c.Is(TypeShape.Struct) && c.Text == "Enumerator");
        Assert.Contains(await coreLib.ReadRootAsync().ListChildrenAsync(), c => c.Subject is NodeSubject.Resources);
    }
}
