using ILSight.Core;
using ILSight.CodeViewArea;

namespace ILSight.App.Tests;

public sealed class NavigationHistoryTests
{
    [Fact]
    public void Working_in_the_same_diagram_again_records_it_once()
    {
        var diagram = new TypeDiagram();
        var history = new NavigationHistory();

        history.RecordDiagram(1, diagram, new TabTitle.Diagram("Vehicle"));
        history.RecordDiagram(1, diagram, new TabTitle.Diagram("Vehicle"));

        Assert.Equal(new TabTitle.Diagram("Vehicle"), UnionAsserts.GetCase<HistoryEntry.Diagram>(Assert.Single(history.Entries)).Title);
    }

    [Fact]
    public async Task A_position_read_back_is_kept_on_the_entry_left_from_that_tab_and_not_on_another_tab_left_at_the_same_version()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var history = new NavigationHistory();
        var first = new TabVersion { TabId = 1, Version = 1 };
        var second = new TabVersion { TabId = 2, Version = 1 };
        var outline = await d.FixtureAsync("Outline");
        history.RecordPlace(outline, DecompilationLanguage.CSharp);
        history.LeaveEntry(DecompilationLanguage.CSharp, AtLine(5), first);
        var greeter = await d.FixtureAsync("Greeter");
        history.RecordPlace(greeter, DecompilationLanguage.CSharp);
        history.LeaveEntry(DecompilationLanguage.CSharp, AtLine(7), second);

        history.KeepPosition(second, AtLine(9));

        var entries = history.Entries.Select(static e => UnionAsserts.GetCase<HistoryEntry.Node>(e)).ToList();
        Assert.Equal(AtLine(5), entries[0].Position);
        Assert.Equal(AtLine(9), entries[1].Position);
    }

    [Fact]
    public async Task A_position_read_back_for_what_no_entry_was_left_from_changes_no_entry()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var history = new NavigationHistory();
        var outline = await d.FixtureAsync("Outline");
        history.RecordPlace(outline, DecompilationLanguage.CSharp);
        history.LeaveEntry(DecompilationLanguage.CSharp, AtLine(5), new TabVersion { TabId = 1, Version = 1 });

        history.KeepPosition(new TabVersion { TabId = 1, Version = 2 }, AtLine(9));

        Assert.Equal(AtLine(5), UnionAsserts.GetCase<HistoryEntry.Node>(Assert.Single(history.Entries)).Position);
    }

    private static ViewPosition AtLine(int line)
    {
        return ViewPosition.OfCode(new CodeViewPosition
        {
            TopLine = line,
            ScrollX = 0f,
            Anchor = new TextPosition
            {
                Line = line,
                Column = 0,
            },
            Caret = new TextPosition
            {
                Line = line,
                Column = 0,
            },
        });
    }
}
