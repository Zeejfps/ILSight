using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.DiagramArea;
using static ILSight.DiagramArea.Diagrams;
using static ILSight.Docking;

namespace ILSight.App.Tests;

public sealed class DiagramTests
{
    private static async Task<CodeReference.Entity> TypeOfAsync(AppStateDriver d, string type)
    {
        return (await d.FixtureAsync(type)).Definition;
    }

    private static List<string> CardNames(TypeDiagram diagram)
    {
        return diagram.Cards
            .Select(static c => c.Name)
            .Order()
            .ToList();
    }

    private static async Task<TypeDiagram> ShowInNewAsync(AppStateDriver d, string type)
    {
        DiagramView.ShowInNewDiagramLater(d.Ui, await TypeOfAsync(d, type), Names.Diagram(await TypeOfAsync(d, type)));
        d.Settle();
        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var diagram = tab.Diagram;
        Assert.NotNull(diagram);
        return diagram;
    }

    /// <summary>Shows the fixture <paramref name="type"/> on the diagram last worked in, as the views ask for it, and
    /// waits until its cards are in.</summary>
    private static async Task ShowInAsync(AppStateDriver d, string type)
    {
        DiagramView.ShowInDiagramLater(d.Ui, await TypeOfAsync(d, type), Names.Diagram(await TypeOfAsync(d, type)));
        d.Settle();
    }

    private static List<DocumentTab> ListDiagramTabs(AppStateDriver d)
    {
        return d.AppState.Navigation
            .OpenTabs.Where(static t => t.Diagram is not null)
            .ToList();
    }

    [Fact]
    public async Task Show_in_diagram_opens_a_tab_with_the_type_and_the_types_of_its_assembly_it_is_related_to()
    {
        var d = AppStateDriver.Opened();

        await ShowInAsync(d, "Vehicle");

        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.Equal("Vehicle diagram", tab.Title);
        Assert.NotNull(tab.Diagram);
        Assert.Equal("Vehicle", tab.Diagram.Cards[0].Name);
        Assert.Equal(["Bike", "Car", "Garage", "Vehicle"], CardNames(tab.Diagram));
        Assert.Same(tab, d.AppState.Navigation.DiagramTab);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public async Task The_links_drawn_are_those_between_the_cards_shown()
    {
        var d = AppStateDriver.Opened();

        var diagram = await ShowInNewAsync(d, "Vehicle");

        Assert.Equal(4, diagram.Links.Count);
        Assert.Contains(new TypeLink.Inherits { From = await TypeOfAsync(d, "Car"), To = await TypeOfAsync(d, "Vehicle") }, diagram.Links);
        Assert.Contains(new TypeLink.HoldsMany { From = await TypeOfAsync(d, "Garage"), To = await TypeOfAsync(d, "Vehicle"), Property = "Vehicles" }, diagram.Links);
        Assert.Contains(new TypeLink.HoldsOne { From = await TypeOfAsync(d, "Vehicle"), To = await TypeOfAsync(d, "Garage"), Property = "Garage" }, diagram.Links);
        var driven = await TypeOfAsync(d, "IDriven");
        Assert.DoesNotContain(diagram.Links, l => l.To == driven);
    }

    [Fact]
    public async Task Showing_another_type_in_the_diagram_adds_it_to_the_same_tab_brought_to_the_front()
    {
        var d = AppStateDriver.Opened();
        await ShowInAsync(d, "Vehicle");
        var diagramTab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(diagramTab);
        d.Show(await d.FixtureAsync("Greeter"));

        await ShowInAsync(d, "Counter");

        Assert.Same(diagramTab, d.AppState.Navigation.ActiveTab);
        Assert.Single(ListDiagramTabs(d));
        Assert.NotNull(diagramTab.Diagram);
        Assert.Equal(["Bike", "Car", "Counter", "Garage", "Vehicle"], CardNames(diagramTab.Diagram));
    }

    [Fact]
    public async Task Show_in_new_diagram_starts_another_which_types_shown_in_a_diagram_then_go_to()
    {
        var d = AppStateDriver.Opened();
        var first = await ShowInNewAsync(d, "Vehicle");

        var second = await ShowInNewAsync(d, "Greeter");
        await ShowInAsync(d, "Counter");

        Assert.Equal(2, ListDiagramTabs(d).Count);
        Assert.Equal(["Counter", "Greeter"], CardNames(second));
        Assert.Equal(4, first.Cards.Count);
    }

    [Fact]
    public async Task A_new_diagram_takes_the_active_tab_while_it_shows_nothing()
    {
        var d = AppStateDriver.Opened();
        var empty = d.AppState.Navigation.OpenEmptyTab();
        Assert.Null(d.AppState.Navigation.FocusTab(empty, d.AppState.Decompiler.Setup));
        var tabs = d.AppState.Navigation.OpenTabs.Count;

        await ShowInNewAsync(d, "Greeter");

        Assert.Equal(tabs, d.AppState.Navigation.OpenTabs.Count);
        Assert.NotNull(empty.Diagram);
    }

    [Fact]
    public async Task A_card_grows_the_diagram_by_its_bases_interfaces_held_types_and_derived_types()
    {
        var d = AppStateDriver.Opened();
        var diagram = await ShowInNewAsync(d, "Car");
        Assert.Equal(["Car", "IDriven", "Vehicle"], CardNames(diagram));
        var vehicleType = await TypeOfAsync(d, "Vehicle");
        var vehicle = diagram.Cards.Single(c => c.Type == vehicleType);

        d.Start((a, ct) => a.DiagramGrowing.ExpandHeldAsync(diagram, vehicle, ct));
        d.Settle();
        d.Start((a, ct) => a.DiagramGrowing.ExpandDerivedAsync(diagram, vehicle, ct));
        d.Settle();

        Assert.Equal(["Bike", "Car", "Garage", "IDriven", "Vehicle"], CardNames(diagram));
        Assert.Contains(new TypeLink.Inherits { From = await TypeOfAsync(d, "Bike"), To = await TypeOfAsync(d, "Vehicle") }, diagram.Links);
        Assert.Contains(new TypeLink.Implements { From = await TypeOfAsync(d, "Car"), To = await TypeOfAsync(d, "IDriven") }, diagram.Links);
    }

    [Fact]
    public async Task A_base_type_of_another_assembly_is_added_only_when_asked_for()
    {
        var d = AppStateDriver.Opened();
        var diagram = await ShowInNewAsync(d, "Breakdown");
        Assert.Equal(["Breakdown"], CardNames(diagram));
        var breakdown = diagram.Cards[0];
        Assert.True(HasMissingBases(diagram, breakdown));

        d.Start((a, ct) => a.DiagramGrowing.ExpandBasesAsync(diagram, breakdown, ct));
        d.Settle();

        Assert.Equal(["Breakdown", "Exception"], CardNames(diagram));
        Assert.False(HasMissingBases(diagram, breakdown));
        Assert.Contains(diagram.Links, static l => l is TypeLink.Inherits);
        Assert.False(Diagrams.IsHome(diagram, diagram.Cards[1].AssemblyPath));
        Assert.True(Diagrams.IsHome(diagram, breakdown.AssemblyPath));
    }

    [Fact]
    public async Task The_implementations_of_an_interface_are_its_derived_types()
    {
        var d = AppStateDriver.Opened();
        var diagram = await ShowInNewAsync(d, "IDriven");

        var idriven = diagram.Cards[0];
        d.Start((a, ct) => a.DiagramGrowing.ExpandDerivedAsync(diagram, idriven, ct));
        d.Settle();

        Assert.Equal(["Car", "IDriven"], CardNames(diagram));
        Assert.Equal([new TypeLink.Implements { From = await TypeOfAsync(d, "Car"), To = await TypeOfAsync(d, "IDriven") }], diagram.Links);
    }

    [Fact]
    public async Task Removing_a_card_takes_its_links_with_it()
    {
        var d = AppStateDriver.Opened();
        var diagram = await ShowInNewAsync(d, "Vehicle");
        var version = diagram.Version;

        RemoveFromDiagram(d.AppState, diagram, await TypeOfAsync(d, "Garage"));

        Assert.Equal(["Bike", "Car", "Vehicle"], CardNames(diagram));
        var garage = await TypeOfAsync(d, "Garage");
        Assert.DoesNotContain(diagram.Links, l => l.From == garage || l.To == garage);
        Assert.NotEqual(version, diagram.Version);
    }

    [Fact]
    public async Task A_newer_layout_supersedes_one_in_flight()
    {
        var d = AppStateDriver.Opened();
        var diagram = await ShowInNewAsync(d, "Vehicle");
        var all = diagram.Cards
            .Select(static c => new MeasuredCard { Type = c.Type, Width = 120f, Height = 80f })
            .ToList();
        var first = new DiagramMeasures { Cards = all, Links = [] };
        var second = new DiagramMeasures { Cards = [all[0]], Links = [] };

        DiagramFaces.LayOutLater(d.Ui, diagram, first);
        DiagramFaces.LayOutLater(d.Ui, diagram, second);
        d.Settle();

        Assert.NotNull(diagram.Layout);
        Assert.Single(diagram.Layout.Places);
        Assert.False(diagram.IsBusy);
    }

    [Fact]
    public async Task Closing_a_diagram_drops_what_it_was_reading()
    {
        var d = AppStateDriver.Opened();
        DiagramView.ShowInNewDiagramLater(d.Ui, await TypeOfAsync(d, "Vehicle"), Names.Diagram(await TypeOfAsync(d, "Vehicle")));
        d.NextFrame();
        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var diagram = tab.Diagram;
        Assert.NotNull(diagram);

        d.AppState.Navigation.CloseTab(tab);
        d.Settle();

        Assert.True(diagram.IsClosed);
        Assert.Empty(diagram.Cards);
        Assert.Null(d.AppState.Navigation.DiagramTab);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public async Task Going_to_code_from_a_diagram_never_replaces_it()
    {
        var d = AppStateDriver.Opened();
        d.TurnOffPreviewTabs();
        await ShowInNewAsync(d, "Vehicle");
        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);

        var greeter = await TypeOfAsync(d, "Greeter");
        d.Start((a, ct) => a.NavigateHandler.HandleAsync(a, greeter, a.Navigation.BeginResolving(inNewTab: false), ct));
        d.Settle();

        Assert.NotSame(tab, d.AppState.Navigation.ActiveTab);
        Assert.NotNull(tab.Diagram);
        Assert.Equal("Greeter", d.AppState.Navigation.ActiveTab?.Node?.Text);
    }

    [Fact]
    public async Task A_type_that_cannot_be_read_is_reported()
    {
        var d = AppStateDriver.Opened();
        var member = (await d.FixtureAsync("Greeter").GetChildAsync("Twice() : int")).Definition;

        DiagramView.ShowInNewDiagramLater(d.Ui, member, Names.Diagram(member));
        d.Settle();

        var error = Assert.Single(d.AppState.Errors.Entries);
        Assert.Equal("Cannot show Twice in a diagram", error.Words().Title);
    }
}
