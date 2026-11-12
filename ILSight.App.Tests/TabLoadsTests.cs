using ILSight.Core;
using ILSight.Core.Comparison;

namespace ILSight.App.Tests;

public sealed class TabLoadsTests
{
    private static DecompileTicket StartLoading(DocumentTabs tabs, BrowseNode node)
    {
        var tab = tabs.AddNew();
        return Decompilation.StartDecompiling(new Queue<ViewCommand>(), tab, node, DecompilationLanguage.CSharp, DecompilerOptions.Default)
            ?? throw new InvalidOperationException("A new tab shows no node yet, so it starts loading one.");
    }

    [Fact]
    public void Loads_put_together_go_to_the_handler_of_their_kind_in_the_order_they_were_put_together()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var tabs = new DocumentTabs();
        var first = StartLoading(tabs, d.Fixture("Greeter"));
        var second = StartLoading(tabs, d.Fixture("Farewell"));
        var decompiled = new List<DecompileTicket>();
        var read = new List<SavedDiagramRead>();
        var found = new List<ReopenedNodesLookup>();
        var compared = new List<ComparisonRun>();
        var restored = new List<AnalysesRestore>();

        var loads = TabLoads.None.With(first).With(TabLoads.From([second], [], [], []));
        loads.Switch(decompiled.Add, read.Add, found.Add, compared.Add, restored.Add);

        Assert.Equal([first, second], decompiled);
        Assert.Empty(read);
        Assert.Empty(found);
        Assert.Empty(compared);
        Assert.Empty(restored);
    }

    [Fact]
    public void No_load_hands_nothing_to_any_handler()
    {
        var handed = 0;

        TabLoads.None.With((DecompileTicket?)null).With((AnalysesRestore?)null).Switch(_ => handed++, _ => handed++, _ => handed++, _ => handed++, _ => handed++);

        Assert.Equal(0, handed);
    }

    [Fact]
    public void A_reopened_comparison_shows_in_its_tab_and_hands_back_its_run_as_a_kind_of_its_own()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var path = d.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == AppStateDriver.FixturePath).FilePath;
        var record = new ComparisonDocumentRecord("document-9", "Fixtures API diff", path, path, ApiScope.AllDeclared, null);
        var tab = d.AppState.Tabs.AddNew();
        Tabs.WaitForComparison(tab, record.Title);
        SavedDocuments.WaitFor(d.AppState.Reopened, tab, record);
        var runs = new List<ComparisonRun>();
        var other = 0;

        var loads = SavedDocuments.ShowReopenedDocuments(d.AppState.Tabs, d.AppState.Reopened, d.AppState.Assemblies);
        loads.Switch(_ => other++, _ => other++, _ => other++, runs.Add, _ => other++);

        var run = Assert.Single(runs);
        Assert.Same(tab.Comparison, run.Comparison);
        Assert.Equal(ApiScope.AllDeclared, run.Scope);
        Assert.True(run.Comparison.IsComparing);
        Assert.Equal(0, other);
        Assert.Empty(d.AppState.Reopened.Tabs);
    }
}
