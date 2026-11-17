using ILSight.Core;
using ILSight.Core.DiagramArea;
using ILSight.DiagramArea;
using ILSight.DocumentViewArea;

namespace ILSight.App.Tests;

public sealed class DiagramTests
{
    private static async Task<CodeReference.Entity> TypeOfAsync(AppStateDriver d, string type)
    {
        var fixture = await d.FixtureAsync(type);
        return fixture.Definition;
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
        var entity = await TypeOfAsync(d, type);
        DiagramView.ShowInNewDiagramLater(d.Ui.AppCommands, entity, Names.Diagram(entity));
        d.Settle();
        var tab = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var diagram = tab.Diagram;
        Assert.NotNull(diagram);
        return diagram;
    }

    private static async Task ShowInAsync(AppStateDriver d, string type)
    {
        var entity = await TypeOfAsync(d, type);
        DiagramView.ShowInDiagramLater(d.Ui.AppCommands, entity, Names.Diagram(entity));
        d.Settle();
    }

    private static List<DocumentTab> ListDiagramTabs(AppStateDriver d)
    {
        return d.AppState.ListSession.Navigation
            .OpenTabs.Where(static t => t.Diagram is not null)
            .ToList();
    }

    [Fact]
    public async Task A_diagrams_tab_is_titled_in_the_language_chosen_after_it_opened_as_is_its_problem()
    {
        var app = AppDriver.Opened();
        var vehicleNode = await app.FixtureAsync("Vehicle");
        var vehicle = vehicleNode.Definition;
        DiagramView.ShowInNewDiagramLater(app.Ui.AppCommands, vehicle, Names.Diagram(vehicle));
        app.Settle(2);

        app.AppState.Locale = Locale.Ja;
        app.Run(2);

        var japanese = Strings.For(Locale.Ja);
        var tab = app.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.NotNull(tab.Diagram);
        var shown = DocumentsView.Tab(app.Ui.Appearance, app.Ui.Tree, app.AppState.ListSession.Navigation, tab);
        Assert.Equal(japanese.DiagramTitle("Vehicle"), shown.Title);
        Assert.Equal(japanese.DiagramTitle("Vehicle"), shown.Tooltip);
        Assert.NotEqual(Strings.En.DiagramTitle("Vehicle"), shown.Title);
        var problem = new AppProblem.DiagramNotReopened { Diagram = Names.Diagram(vehicle), Error = new IOException("gone") };
        Assert.Equal(japanese.ErrorShowInDiagram(japanese.DiagramTitle("Vehicle")), ProblemTexts.Describe(japanese, problem).Title);
    }

    [Fact]
    public async Task Show_in_diagram_opens_a_tab_with_the_type_and_the_types_of_its_assembly_it_is_related_to()
    {
        var d = AppStateDriver.Opened();

        await ShowInAsync(d, "Vehicle");

        var tab = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.Equal("Vehicle diagram", tab.TitleIn(Strings.En));
        Assert.NotNull(tab.Diagram);
        Assert.Equal("Vehicle", tab.Diagram.Cards[0].Name);
        Assert.Equal(["Bike", "Car", "Garage", "Vehicle"], CardNames(tab.Diagram));
        Assert.Same(tab, d.AppState.ListSession.Navigation.DiagramTab);
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
        var diagramTab = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(diagramTab);
        var greeter = await d.FixtureAsync("Greeter");
        d.Show(greeter);

        await ShowInAsync(d, "Counter");

        Assert.Same(diagramTab, d.AppState.ListSession.Navigation.ActiveTab);
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
        var empty = d.AppState.ListSession.Navigation.OpenEmptyTab();
        var decompileTicket = d.AppState.ListSession.Navigation.FocusTab(empty, d.AppState.Decompiler.Setup);
        Assert.Null(decompileTicket);
        var tabs = d.AppState.ListSession.Navigation.OpenTabs.Count;

        await ShowInNewAsync(d, "Greeter");

        Assert.Equal(tabs, d.AppState.ListSession.Navigation.OpenTabs.Count);
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

        d.Start((a, ct) =>
        {
            var expansion = ExpansionRead.Start(diagram, vehicle);
            return a.DiagramGrower.ExpandHeldAsync(expansion, ct);
        });
        d.Settle();
        d.Start((a, ct) =>
        {
            var expansion = ExpansionRead.Start(diagram, vehicle);
            return a.DiagramGrower.ExpandDerivedAsync(expansion, ct);
        });
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
        Assert.True(Diagrams.HasMissingBases(diagram, breakdown));

        d.Start((a, ct) =>
        {
            var expansion = ExpansionRead.Start(diagram, breakdown);
            return a.DiagramGrower.ExpandBasesAsync(expansion, ct);
        });
        d.Settle();

        Assert.Equal(["Breakdown", "Exception"], CardNames(diagram));
        Assert.False(Diagrams.HasMissingBases(diagram, breakdown));
        Assert.Contains(diagram.Links, static l => l is TypeLink.Inherits);
        Assert.False(diagram.IsHome(diagram.Cards[1].AssemblyPath));
        Assert.True(diagram.IsHome(breakdown.AssemblyPath));
    }

    [Fact]
    public async Task The_implementations_of_an_interface_are_its_derived_types()
    {
        var d = AppStateDriver.Opened();
        var diagram = await ShowInNewAsync(d, "IDriven");

        var idriven = diagram.Cards[0];
        d.Start((a, ct) =>
        {
            var expansion = ExpansionRead.Start(diagram, idriven);
            return a.DiagramGrower.ExpandDerivedAsync(expansion, ct);
        });
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

        var garageType = await TypeOfAsync(d, "Garage");
        diagram.RemoveCard(garageType);

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

        DiagramFaces.LayOutLater(d.Ui.AppCommands, diagram, first);
        DiagramFaces.LayOutLater(d.Ui.AppCommands, diagram, second);
        d.Settle();

        Assert.NotNull(diagram.Layout);
        Assert.Single(diagram.Layout.Places);
        Assert.False(diagram.IsBusy);
    }

    [Fact]
    public async Task Closing_a_diagram_drops_what_it_was_reading()
    {
        var d = AppStateDriver.Opened();
        var vehicleType = await TypeOfAsync(d, "Vehicle");
        DiagramView.ShowInNewDiagramLater(d.Ui.AppCommands, vehicleType, Names.Diagram(vehicleType));
        d.NextFrame();
        var tab = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var diagram = tab.Diagram;
        Assert.NotNull(diagram);

        d.AppState.ListSession.Navigation.CloseTab(tab);
        d.Settle();

        Assert.True(diagram.IsClosed);
        Assert.Empty(diagram.Cards);
        Assert.Null(d.AppState.ListSession.Navigation.DiagramTab);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public async Task Going_to_code_from_a_diagram_never_replaces_it()
    {
        var d = AppStateDriver.Opened();
        d.TurnOffPreviewTabs();
        await ShowInNewAsync(d, "Vehicle");
        var tab = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);

        var greeter = await TypeOfAsync(d, "Greeter");
        d.Start((a, ct) =>
        {
            var resolveTicket = a.ListSession.Navigation.BeginResolving(inNewTab: false);
            return a.NavigateHandler.HandleAsync(a.ListSession.Navigation, a.Decompiler, greeter, resolveTicket, ct);
        });
        d.Settle();

        Assert.NotSame(tab, d.AppState.ListSession.Navigation.ActiveTab);
        Assert.NotNull(tab.Diagram);
        Assert.Equal("Greeter", d.AppState.ListSession.Navigation.ActiveTab?.Node?.Text);
    }

    [Fact]
    public async Task What_is_no_type_that_can_be_read_is_refused_on_the_refusal_line_not_reported()
    {
        var d = AppStateDriver.Opened();
        var greeter = await d.FixtureAsync("Greeter");
        var twice = await greeter.GetChildAsync("Twice() : int");
        var member = twice.Definition;

        DiagramView.ShowInNewDiagramLater(d.Ui.AppCommands, member, Names.Diagram(member));
        d.Settle();

        var refused = Assert.IsType<Refusal.TypeNotDiagrammed>(d.AppState.Refusals.Shown);
        Assert.Equal("Cannot show Twice in a diagram", refused.Words(d.Ui.Appearance.Strings).What);
        Assert.Empty(d.AppState.Errors.Entries);
    }
}
