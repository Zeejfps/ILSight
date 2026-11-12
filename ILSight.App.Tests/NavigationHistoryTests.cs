using ILSight.Core;
using ILSight.CodeView;

namespace ILSight.App.Tests;

public sealed class NavigationHistoryTests
{
    private static NavigationHistory Holding(params TypeDiagram[] diagrams)
    {
        var history = new NavigationHistory();
        foreach (var diagram in diagrams)
        {
            history.RecordDiagram(diagram, "diagram");
        }

        return history;
    }

    private static List<TypeDiagram> Shown(NavigationHistory history)
    {
        return history.Entries
            .OfType<HistoryEntry.Diagram>()
            .Select(static e => e.Shown)
            .ToList();
    }

    [Fact]
    public void Working_in_the_same_diagram_again_records_it_once()
    {
        var diagram = new TypeDiagram();

        var history = Holding(diagram, diagram);

        Assert.Single(history.Entries);
    }

    [Fact]
    public void Forgetting_a_diagram_drops_each_of_its_entries_and_keeps_the_current_one_current()
    {
        var (first, second, third) = (new TypeDiagram(), new TypeDiagram(), new TypeDiagram());
        var history = Holding(first, second, first, third);

        history.ForgetDiagram(first);

        Assert.Equal([second, third], Shown(history));
        Assert.Equal(1, history.CurrentIndex);
    }

    [Fact]
    public void Forgetting_the_current_diagram_makes_the_entry_before_it_current()
    {
        var (first, second) = (new TypeDiagram(), new TypeDiagram());
        var history = Holding(first, second);

        history.ForgetDiagram(second);

        Assert.Equal([first], Shown(history));
        Assert.Equal(0, history.CurrentIndex);
    }

    [Fact]
    public void Forgetting_the_only_diagram_leaves_no_entry_current()
    {
        var diagram = new TypeDiagram();
        var history = Holding(diagram);

        history.ForgetDiagram(diagram);

        Assert.Empty(history.Entries);
        Assert.Equal(-1, history.CurrentIndex);
    }
    [Fact]
    public void A_position_read_back_is_kept_on_the_entry_left_from_that_tab_and_not_on_another_tab_left_at_the_same_version()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var history = new NavigationHistory();
        var first = new TabVersion { TabId = 1, Version = 1 };
        var second = new TabVersion { TabId = 2, Version = 1 };
        history.RecordPlace(d.Fixture("Outline"), DecompilationLanguage.CSharp);
        history.LeaveEntry(DecompilationLanguage.CSharp, AtLine(5), first);
        history.RecordPlace(d.Fixture("Greeter"), DecompilationLanguage.CSharp);
        history.LeaveEntry(DecompilationLanguage.CSharp, AtLine(7), second);

        history.KeepPosition(second, AtLine(9));

        var entries = history.Entries.Cast<HistoryEntry.Node>().ToList();
        Assert.Equal(AtLine(5), entries[0].Position);
        Assert.Equal(AtLine(9), entries[1].Position);
    }

    [Fact]
    public void A_position_read_back_for_what_no_entry_was_left_from_changes_no_entry()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var history = new NavigationHistory();
        history.RecordPlace(d.Fixture("Outline"), DecompilationLanguage.CSharp);
        history.LeaveEntry(DecompilationLanguage.CSharp, AtLine(5), new TabVersion { TabId = 1, Version = 1 });

        history.KeepPosition(new TabVersion { TabId = 1, Version = 2 }, AtLine(9));

        Assert.Equal(AtLine(5), Assert.IsType<HistoryEntry.Node>(Assert.Single(history.Entries)).Position);
    }

    private static ViewPosition AtLine(int line)
    {
        return ViewPosition.OfCode(new CodeViewPosition(line, 0f, new TextPosition(line, 0), new TextPosition(line, 0)));
    }
}
