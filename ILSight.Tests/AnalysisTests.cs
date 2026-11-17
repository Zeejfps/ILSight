using ICSharpCode.Decompiler.TypeSystem;
using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ILSight.Core;
using ILSight.Core.AnalysisArea;

namespace ILSight.Tests;

public sealed class AnalysisTests
{
    private static async Task<(AssemblyList List, BrowseNode Node)> MemberAsync(string label)
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var calculator = await assembly.CalculatorAsync();
        var node = await calculator.GetChildAsync(label);
        return (list, node);
    }

    private static List<AnalysisSymbol> Run(AssemblyList list, BrowseNode node, string header)
    {
        var symbol = node.Analysis;
        Assert.NotNull(symbol);
        return Analyzers.RunAsync(list, symbol, symbol.Kinds.Single(k => Analyzers.GetHeader(k) == header))
            .ToBlockingEnumerable().SelectMany(static b => b.GetValue()).Select(static f => Assert.IsType<AnalysisFind.Found>(f).Symbol).ToList();
    }

    [Fact]
    public async Task A_method_offers_ILSpys_analyses_in_its_order()
    {
        var (_, add) = await MemberAsync("Add(T) : void");

        var symbol = add.Analysis;
        Assert.NotNull(symbol);
        var kinds = symbol.Kinds.Select(Analyzers.GetHeader);

        Assert.Equal(["Uses", "Used By", "Implements"], kinds);
    }

    [Fact]
    public async Task A_saved_token_the_module_has_no_row_for_is_asked_and_not_found()
    {
        var (list, _) = Trees.OpenTestAssembly();
        var path = AssemblyPath.Of(Trees.TestAssemblyPath);
        var pastTheEnd = new CodeAnchor { Kind = CodeAnchorKind.Definition, FilePath = path, Token = 0x06FFFFFF };
        var noEntities = new CodeAnchor { Kind = CodeAnchorKind.Definition, FilePath = path, Token = 0x23000001 };

        UnionAsserts.GetCase<Lookup<AnalysisSymbol>.NotFound>((await Analyzers.FindAsync(list, pastTheEnd, CancellationToken.None)).GetValue());
        UnionAsserts.GetCase<Lookup<AnalysisSymbol>.NotFound>((await Analyzers.FindAsync(list, noEntities, CancellationToken.None)).GetValue());
    }

    [Fact]
    public async Task A_saved_symbol_in_a_file_that_cannot_be_read_fails_one_in_a_file_gone_is_not_found()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var path = temp.Write("notes.dll", "not an assembly");
        var unreadable = new CodeAnchor { Kind = CodeAnchorKind.Definition, FilePath = AssemblyPath.Of(path), Token = 0x06000001 };
        var gone = new CodeAnchor { Kind = CodeAnchorKind.Definition, FilePath = AssemblyPath.Of(temp.PathOf("gone.dll")), Token = 0x06000001 };

        Assert.IsType<AssemblyLoadException>(UnionAsserts.GetCase<Outcome<Lookup<AnalysisSymbol>>.Failed>(await Analyzers.FindAsync(list, unreadable, CancellationToken.None)).Error);
        UnionAsserts.GetCase<Lookup<AnalysisSymbol>.NotFound>((await Analyzers.FindAsync(list, gone, CancellationToken.None)).GetValue());
    }

    [Fact]
    public async Task A_saved_symbol_the_library_cannot_read_says_its_module_unreadable_with_why()
    {
        using var temp = new TempDir();
        var grow = BrokenAssemblies.GetMethod(Trees.TestAssemblyPath, "Circle", "Grow");
        var unreadablePath = BrokenAssemblies.WriteUnreadableMethodName(temp, Trees.TestAssemblyPath, grow);
        var path = AssemblyPath.Of(unreadablePath);
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var anchor = new CodeAnchor { Kind = CodeAnchorKind.Definition, FilePath = path, Token = MetadataTokens.GetToken(grow) };

        var found = await Analyzers.FindAsync(list, anchor, CancellationToken.None);

        var unreadable = UnionAsserts.GetCase<Lookup<AnalysisSymbol>.Unreadable>(found.GetValue());
        Assert.Equal(path, unreadable.Module.Module.FilePath);
        Assert.IsType<BadImageFormatException>(unreadable.Module.Why);
    }

    [Fact]
    public async Task An_analysis_reaching_a_member_the_library_cannot_read_names_its_module_partly_read_saying_why()
    {
        using var temp = new TempDir();
        var add = BrokenAssemblies.GetMethod(Trees.TestAssemblyPath, "Calculator`1", "Add");
        var plus = BrokenAssemblies.GetMethod(Trees.TestAssemblyPath, "Calculator`1", "op_Addition");
        var unreadablePath = BrokenAssemblies.WriteUnreadableMethodName(temp, Trees.TestAssemblyPath, plus);
        var path = AssemblyPath.Of(unreadablePath);
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        list.OpenModule(path.Text);
        var anchor = new CodeAnchor { Kind = CodeAnchorKind.Definition, FilePath = path, Token = MetadataTokens.GetToken(add) };
        var symbol = UnionAsserts.GetCase<Lookup<AnalysisSymbol>.Found>((await Analyzers.FindAsync(list, anchor, CancellationToken.None)).GetValue()).Value;
        var usedBy = symbol.Kinds.Single(static k => Analyzers.GetHeader(k) == "Used By");

        var batches = new List<Outcome<IReadOnlyList<AnalysisFind>>>();
        await foreach (var batch in Analyzers.RunAsync(list, symbol, usedBy, CancellationToken.None))
        {
            batches.Add(batch);
        }

        var found = batches.SelectMany(static b => b.GetValue()).ToList();
        Assert.DoesNotContain(found, static f => f is AnalysisFind.Found { Symbol.Label: var label } && label.StartsWith("operator +", StringComparison.Ordinal));
        var partly = Assert.IsType<AnalysisFind.PartlyRead>(Assert.Single(found, static f => f is AnalysisFind.PartlyRead));
        Assert.Equal("UnreadableMethodName.dll", partly.Name);
        Assert.IsType<BadImageFormatException>(partly.Error);
        Assert.Equal(partly.Why, partly.Error.Message);
    }

    [Fact]
    public async Task Used_by_finds_the_callers_and_each_leads_to_its_node()
    {
        var (list, add) = await MemberAsync("Add(T) : void");

        var callers = Run(list, add, "Used By");

        var plus = Assert.Single(callers);
        Assert.Equal(new SymbolShape.Member(MemberShape.Operator), plus.Shape);
        Assert.StartsWith("operator +", plus.Label);
        Assert.Equal(new SymbolLocation { Container = Trees.FixturesNamespace + ".Calculator<T>", Assembly = "ILSight.Tests" }, plus.Location);
        var calculator = add.Parent;
        Assert.NotNull(calculator);
        var children = await calculator.ListChildrenAsync();
        Assert.Same(children.Single(c => c.Is(MemberShape.Operator)), await list.FindNodeAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(plus.Reference)));
    }

    [Fact]
    public async Task Read_by_finds_every_member_reading_a_field()
    {
        var (list, items) = await MemberAsync("_items : List<T>");

        var readers = Run(list, items, "Read By").Select(r => r.Label).ToList();

        Assert.Contains(readers, l => l.StartsWith("Add("));
        Assert.Contains(readers, l => l.StartsWith("TryGet("));
        Assert.Contains(readers, l => l.StartsWith("get_Count") || l.StartsWith("Count"));
    }

    [Fact]
    public async Task An_interface_is_used_by_the_types_implementing_it_and_its_property_by_theirs()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var fixtures = await assembly.FixturesAsync();
        var shape = await fixtures.GetChildAsync("IShape");
        var area = await shape.GetChildAsync("Area : double");

        var users = Run(list, shape, "Used By").Select(r => r.Label);
        var implementations = Run(list, area, "Implemented By").Select(r => r.Label);
        var areaSymbol = area.Analysis;
        Assert.NotNull(areaSymbol);
        var accessors = areaSymbol.Accessors.Select(a => a.Label);

        Assert.Contains("Circle", users);
        Assert.Contains(implementations, l => l.StartsWith("Area"));
        Assert.Contains(accessors, l => l.StartsWith("get_Area"));
    }

    [Fact]
    public async Task An_analysis_over_a_module_the_library_cannot_read_shows_the_others_results_and_names_it()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var core = list.OpenModule(typeof(AssemblyList).Assembly.Location);
        var unreadablePath = BrokenAssemblies.WriteUnreadableTypeSystem(temp, Trees.TestAssemblyPath);
        list.OpenModule(unreadablePath);
        var coreRoot = await core.ReadRootAsync();
        var assemblyPath = await coreRoot.GetChildAsync("ILSight.Core", "AssemblyPath");
        var symbol = assemblyPath.Analysis;
        Assert.NotNull(symbol);
        var usedBy = symbol.Kinds.Single(static k => Analyzers.GetHeader(k) == "Used By");

        var found = new List<AnalysisFind>();
        await foreach (var batch in Analyzers.RunAsync(list, symbol, usedBy, CancellationToken.None))
        {
            found.AddRange(batch.GetValue());
        }

        Assert.Contains(found, static f => f is AnalysisFind.Found);
        var unreadable = Assert.IsType<AnalysisFind.Unreadable>(Assert.Single(found, static f => f is AnalysisFind.Unreadable));
        Assert.Equal("UnreadableTypes.dll", unreadable.Name);
        Assert.NotEmpty(unreadable.Why);
        Assert.Equal(unreadable.Why, unreadable.Error.Message);
    }

    [Fact]
    public async Task An_analysis_over_a_module_read_in_part_shows_what_it_found_and_names_the_module_partly_read()
    {
        using var temp = new TempDir();
        var tryGet = BrokenAssemblies.GetMethod(Trees.TestAssemblyPath, "Calculator`1", "TryGet");
        var unreadablePath = BrokenAssemblies.WriteUnreadableMethodBody(temp, Trees.TestAssemblyPath, tryGet);
        var path = AssemblyPath.Of(unreadablePath);
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        list.OpenModule(path.Text);
        var items = typeof(Fixtures.Calculator<>).GetField("_items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(items);
        var anchor = new CodeAnchor { Kind = CodeAnchorKind.Definition, FilePath = path, Token = items.MetadataToken };
        var symbol = UnionAsserts.GetCase<Lookup<AnalysisSymbol>.Found>((await Analyzers.FindAsync(list, anchor, CancellationToken.None)).GetValue()).Value;
        var readBy = symbol.Kinds.Single(static k => Analyzers.GetHeader(k) == "Read By");

        var found = new List<AnalysisFind>();
        await foreach (var batch in Analyzers.RunAsync(list, symbol, readBy, CancellationToken.None))
        {
            found.AddRange(batch.GetValue());
        }

        var readers = found.OfType<AnalysisFind.Found>().Select(static f => f.Symbol.Label).ToList();
        Assert.Contains(readers, static l => l.StartsWith("Add(", StringComparison.Ordinal));
        Assert.DoesNotContain(readers, static l => l.StartsWith("TryGet(", StringComparison.Ordinal));
        var partly = Assert.IsType<AnalysisFind.PartlyRead>(Assert.Single(found, static f => f is AnalysisFind.PartlyRead));
        Assert.Equal("UnreadableMethodBody.dll", partly.Name);
        Assert.Equal(1, partly.Count);
        Assert.NotEmpty(partly.Why);
        Assert.Equal(partly.Why, partly.Error.Message);
    }

    [Fact]
    public async Task A_scope_scans_the_list_as_it_stood_when_its_reads_were_awaited()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var core = list.OpenModule(typeof(AssemblyList).Assembly.Location);
        using var reading = LoadedAssemblies.BeginReading();
        var reads = await AnalyzerScope.ReadReachableAsync(reading, list, core, CancellationToken.None);
        list.OpenModule(Trees.TestAssemblyPath);
        var coreRoot = await core.ReadRootAsync();
        var assemblyPath = await coreRoot.GetChildAsync("ILSight.Core", "AssemblyPath");
        var type = Assert.IsAssignableFrom<IEntity>(NodeDefinitions.GetSymbol(UnionAsserts.GetCase<NodeSubject.TypeDefinition>(assemblyPath.Subject).Definition));

        var scanned = AnalyzerScope.Create(list, reads, new ConcurrentDictionary<LoadedModule, UnreadableModule>(), type).ListTypeSystems(CancellationToken.None).ToList();

        Assert.Equal([core.FilePath.FileName], scanned.Select(static t => Path.GetFileName(t.MainModule.MetadataFile.FileName)));
    }

    [Fact]
    public async Task A_bug_in_which_analyses_apply_is_the_apps_not_the_symbol_unreadable_and_the_table_decides_on_facts_read_out()
    {
        var (list, _) = Trees.OpenTestAssembly();
        var add = BrokenAssemblies.GetMethod(Trees.TestAssemblyPath, "Calculator`1", "Add");
        var anchor = new CodeAnchor { Kind = CodeAnchorKind.Definition, FilePath = AssemblyPath.Of(Trees.TestAssemblyPath), Token = MetadataTokens.GetToken(add) };
        var asked = new List<OfferFacts>();
        var recording = new AnalysisKind
        {
            Name = AnalysisName.Uses, Order = 1, Prepare = static _ => null,
            Applies = f =>
            {
                asked.Add(f);
                return f.HasBody;
            },
        };
        var buggy = new AnalysisKind
        {
            Name = AnalysisName.UsedBy, Order = 2, Prepare = static _ => null, Applies = static _ => throw new InvalidOperationException("the table's bug"),
        };

        var offered = UnionAsserts.GetCase<Lookup<AnalysisSymbol>.Found>((await Analyzers.FindAsync(list, anchor, [recording], CancellationToken.None)).GetValue()).Value;
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Analyzers.FindAsync(list, anchor, [recording, buggy], CancellationToken.None));

        Assert.Equal("the table's bug", thrown.Message);
        Assert.Same(recording, Assert.Single(offered.Kinds));
        var facts = asked[0];
        Assert.Equal(AnalyzedSort.Method, facts.Sort);
        Assert.Equal(SymbolKind.Method, facts.SymbolKind);
        Assert.True(facts.HasBody);
        Assert.Equal(TypeKind.Class, facts.DeclaringDefinitionKind);
    }

    [Fact]
    public async Task A_bug_in_the_table_never_fails_the_listing_of_a_types_members_and_fails_the_ask_for_a_members_analyses()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var session = new BrowseSession();
        var calculator = await assembly.CalculatorAsync();
        var buggy = new AnalysisKind
        {
            Name = AnalysisName.UsedBy, Order = 1, Prepare = static _ => null, Applies = static _ => throw new InvalidOperationException("the table's bug"),
        };

        var childrenState = await BrowseSessions.GetChildrenAsync(session, calculator);
        var listed = UnionAsserts.GetCase<ChildrenState.Ready>(childrenState).Children;
        var add = listed.Single(static c => c.Text == "Add(T) : void");
        var facts = Assert.IsType<AnalysisFacts>(add.AnalysisFacts);
        var thrown = Assert.Throws<InvalidOperationException>(() => Analyzers.Compose(facts, [buggy]));

        Assert.Equal("the table's bug", thrown.Message);
        Assert.Contains(listed, static c => c.Text == "Describe() : string");
        Assert.Same(add.Analysis, add.Analysis);
        Assert.NotEmpty(Assert.IsType<AnalysisSymbol>(add.Analysis).Kinds);
    }

    [Fact]
    public async Task An_enum_in_an_attribute_whose_underlying_type_is_unknown_is_bad_data_said_outside_the_decoders_count()
    {
        var read = await LoadedAssemblies.ReadTypeSystemAheadAsync(Trees.TestAssembly, CancellationToken.None);
        var main = UnionAsserts.GetCase<TypeSystemRead.Read>(read).TypeSystem.MainModule;
        var file = main.MetadataFile;
        var callbacks = new CallbackFailure();
        var decoder = new FindTypeInAttributeDecoder
        {
            DeclaringModule = file, DeclaringMetadata = file.Metadata, CurrentModule = main, CurrentFile = file, Handle = default, PrimitiveType = 0,
            PassedOver = new PassedOver(), Callbacks = callbacks,
        };

        Assert.Throws<ICSharpCode.Decompiler.Metadata.EnumUnderlyingTypeResolveException>(() => decoder.GetUnderlyingEnumType(TokenSearchResult.NoResult));
        Assert.False(callbacks.Failed);
        Assert.Equal(PrimitiveTypeCode.Int32, decoder.GetUnderlyingEnumType((TokenSearchResult)PrimitiveTypeCode.Int32));
    }

    [Fact]
    public async Task A_bug_in_an_analysiss_own_test_ends_the_run_with_it_not_as_the_librarys_failure()
    {
        var (list, add) = await MemberAsync("Add(T) : void");
        var facts = Assert.IsType<AnalysisFacts>(add.AnalysisFacts);
        var buggy = new AnalysisKind
        {
            Name = AnalysisName.Uses, Order = 1, Applies = static _ => true,
            Prepare = static symbol => _ => FindThenFail(Assert.IsAssignableFrom<IEntity>(symbol)),
        };
        var symbol = Analyzers.Compose(facts, [buggy]);

        var batches = new List<Outcome<IReadOnlyList<AnalysisFind>>>();
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var batch in Analyzers.RunAsync(list, symbol, buggy, CancellationToken.None))
            {
                batches.Add(batch);
            }
        });

        Assert.Equal("the analysis's bug", thrown.Message);
        Assert.DoesNotContain(batches, static b => b is Outcome<IReadOnlyList<AnalysisFind>>.Failed);
    }

    private static IEnumerable<ISymbol> FindThenFail(IEntity analyzed)
    {
        yield return analyzed;
        throw new InvalidOperationException("the analysis's bug");
    }
}
