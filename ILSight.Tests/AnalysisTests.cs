using ICSharpCode.Decompiler.TypeSystem;
using System.Collections.Concurrent;
using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight.Tests;

public sealed class AnalysisTests
{
    private static async Task<(AssemblyList List, BrowseNode Node)> MemberAsync(string label)
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        return (list, await assembly.CalculatorAsync().GetChildAsync(label));
    }

    private static List<AnalysisSymbol> Run(AssemblyList list, BrowseNode node, string header)
    {
        var symbol = node.Analysis;
        Assert.NotNull(symbol);
        return Analyzers.RunAsync(list, symbol, symbol.Kinds.Single(k => Analyzers.GetHeader(k) == header))
            .ToBlockingEnumerable().SelectMany(static b => b).Select(static f => Assert.IsType<AnalysisFind.Found>(f).Symbol).ToList();
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
    public async Task Used_by_finds_the_callers_and_each_leads_to_its_node()
    {
        var (list, add) = await MemberAsync("Add(T) : void");

        var callers = Run(list, add, "Used By");

        var plus = Assert.Single(callers);
        Assert.Equal(new SymbolShape.Member(MemberShape.Operator), plus.Shape);
        Assert.StartsWith("operator +", plus.Label);
        Assert.Equal(Trees.FixturesNamespace + ".Calculator<T> (ILSight.Tests)", plus.Location);
        var calculator = add.Parent;
        Assert.NotNull(calculator);
        Assert.Same((await calculator.ListChildrenAsync()).Single(c => c.Is(MemberShape.Operator)), await list.FindNodeAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(plus.Reference)));
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
        var shape = await assembly.FixturesAsync().GetChildAsync("IShape");
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
        list.OpenModule(BrokenAssemblies.WriteUnreadableTypeSystem(temp, Trees.TestAssemblyPath));
        var symbol = (await core.ReadRootAsync().GetChildAsync("ILSight.Core", "AssemblyPath")).Analysis;
        Assert.NotNull(symbol);
        var usedBy = symbol.Kinds.Single(static k => Analyzers.GetHeader(k) == "Used By");

        var found = new List<AnalysisFind>();
        await foreach (var batch in Analyzers.RunAsync(list, symbol, usedBy, CancellationToken.None))
        {
            found.AddRange(batch);
        }

        Assert.Contains(found, static f => f is AnalysisFind.Found);
        var unreadable = Assert.IsType<AnalysisFind.Unreadable>(Assert.Single(found, static f => f is AnalysisFind.Unreadable));
        Assert.Equal("UnreadableTypes.dll", unreadable.Name);
        Assert.NotEmpty(unreadable.Why);
    }

    [Fact]
    public async Task A_scope_scans_the_list_as_it_stood_when_its_reads_were_awaited()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var core = list.OpenModule(typeof(AssemblyList).Assembly.Location);
        var reads = await AnalyzerScope.ReadReachableAsync(list, core, CancellationToken.None);
        list.OpenModule(Trees.TestAssemblyPath);
        var type = Assert.IsAssignableFrom<IEntity>(NodeDefinitions.GetSymbol(Assert.IsType<NodeSubject.TypeDefinition>((await core.ReadRootAsync().GetChildAsync("ILSight.Core", "AssemblyPath")).Subject).Definition));

        var scanned = AnalyzerScope.Create(list, reads, new ConcurrentDictionary<LoadedModule, UnreadableModule>(), type).ListTypeSystems(CancellationToken.None).ToList();

        Assert.Equal([core.FilePath.FileName], scanned.Select(static t => Path.GetFileName(t.MainModule.MetadataFile.FileName)));
    }
}
