using ILSight.Core;

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

        var loads = TabLoads.None.With(first).With(TabLoads.From([second], [], [], []));
        loads.Switch(decompiled.Add, read.Add, found.Add, compared.Add);

        Assert.Equal([first, second], decompiled);
        Assert.Empty(read);
        Assert.Empty(found);
        Assert.Empty(compared);
    }

    [Fact]
    public void No_load_hands_nothing_to_any_handler()
    {
        var handed = 0;

        TabLoads.None.With((DecompileTicket?)null).Switch(_ => handed++, _ => handed++, _ => handed++, _ => handed++);

        Assert.Equal(0, handed);
    }
}
