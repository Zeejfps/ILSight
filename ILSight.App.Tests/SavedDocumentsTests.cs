using ZGF.Desktop;
using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Diagrams;
using static ILSight.Assemblies;
using static ILSight.AssemblyListKeeping.AssemblyLists;
using static ILSight.Diagramming.Diagrams;
using static ILSight.DocumentPanels;
using static ILSight.App.Tests.TestPaths;
using ILSight.SettingsFormat;
using ILSight.CodeView;
using ILSight.DiagramViewport;
using ILSight.DocumentViewing;
using ILSight.AppSettingsFormat;
using ILSight.AssemblyListKeeping;
using ILSight.MetadataGridArea;
using ILSight.Diagramming;

namespace ILSight.App.Tests;

public sealed class SavedDocumentsTests : IDisposable
{
    private const string Other = "Other";
    private const string MissingPanel = "document-5";
    private const string DiagramPanel = "document-2";

    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    private static CodeViewState CodeOf(AppDriver app, DocumentTab tab)
    {
        return DocumentPanels.GetView(app.Ui.Documents, tab).Code;
    }

    private static DiagramViewState DiagramOf(AppDriver app, DocumentTab tab)
    {
        return DocumentPanels.GetView(app.Ui.Documents, tab).Diagram;
    }

    private static MetadataGridState GridOf(AppDriver app, DocumentTab tab)
    {
        var grid = tab.Grid;
        Assert.NotNull(grid);
        return DocumentPanels.GetGridState(DocumentPanels.GetView(app.Ui.Documents, tab), grid);
    }

    private static DocumentTab GridTab(AppDriver app)
    {
        return app.AppState.Tabs.Open.Single(t => t.Grid is not null);
    }

    /// <summary>An app showing the TypeDef table with Greeter's row selected, saved on exit; the row.</summary>
    private int SaveGridOnGreeter()
    {
        var app = new AppDriver(height: 360f, settings: _temp.Settings());
        app.Driver.OpenFixtures();
        app.Run(3);
        app.Show(app.Node("Metadata", "Tables").Table("TypeDef"), 4);
        var tab = GridTab(app);
        var grid = tab.Grid;
        Assert.NotNull(grid);
        var row = grid.RowNamed("Greeter");
        MetadataGridWidget.RevealRow(GridOf(app, tab), row);
        app.Run(3);
        app.App.SaveOnExit(new WindowBounds(10, 20, 1000, 360, false));
        return row;
    }

    private static DocumentTab TabTitled(AppDriver app, string title)
    {
        return app.AppState.Tabs.Open.Single(t => t.Title == title);
    }

    private static CodeViewPosition ScrollTo(AppDriver app, DocumentTab tab, int topLine, TextPosition anchor, TextPosition caret)
    {
        CodeViewWidget.RestorePosition(CodeOf(app, tab), new CodeViewPosition(topLine, 0f, anchor, caret));
        app.Run(4);
        return CodeOf(app, tab).Position;
    }

    /// <summary>Docks <paramref name="tab"/>'s panel in a group of its own right of <paramref name="beside"/>'s.</summary>
    private static void MoveRightOf(AppDriver app, DocumentTab tab, DocumentTab beside)
    {
        var layout = app.Ui.Layout;
        var group = layout.GroupOf(DocumentPanels.GetPanel(app.Ui.Documents, beside));
        Assert.NotNull(group);
        layout.Dock(DocumentPanels.GetPanel(app.Ui.Documents, tab), group, DockEdge.Right);
        app.Run(2);
    }

    /// <summary>Runs frames until the diagram of <paramref name="tab"/> is laid out and its cards stopped moving.</summary>
    private static void SettleDiagram(AppDriver app, DocumentTab tab)
    {
        app.Frames.WaitUntil(() => IsLaidOut(app, tab), Patience);
        app.Run();
    }

    private static bool IsLaidOut(AppDriver app, DocumentTab tab)
    {
        if (app.AppState.IsDiagramming)
        {
            return false;
        }

        var view = DiagramOf(app, tab);
        var diagram = tab.Diagram;
        Assert.NotNull(diagram);
        if (view.LaidOutVersion != diagram.Version)
        {
            return false;
        }

        if (view.IsFitPending)
        {
            return false;
        }

        return !view.Transition.IsMoving;
    }

    /// <summary>Each open tab's title with the panel it is docked as, in the order the tabs opened.</summary>
    private static List<(string? Title, string Panel)> ListTabPanels(AppDriver app)
    {
        return app.AppState.Tabs
            .Open.Select(t => (t.Title, DocumentPanels.GetPanel(app.Ui.Documents, t)))
            .ToList();
    }

    /// <summary>Each group of the layout as its panels and the one in front.</summary>
    private static List<string> ListGroups(AppDriver app)
    {
        return app.Ui.Layout.AllGroups()
            .Select(static g => $"{string.Join(",", g.Panels)} [{g.Active}]")
            .ToList();
    }

    private static List<TypeAnchor> ListTypes(DocumentTab tab)
    {
        var diagram = tab.Diagram;
        Assert.NotNull(diagram);
        return diagram.Cards
            .Select(static c => c.Anchor)
            .ToList();
    }

    private static AppSettings SettingsWith(DocumentsRecord documents)
    {
        var list = new AssemblyListRecord(SavedAssemblyLists.DefaultName, [PathOf(AppStateDriver.FixturePath)], Documents: documents);
        return new AppSettings { AssemblyLists = [list] };
    }

    private static TypeAnchor FixtureType(string name)
    {
        return new TypeAnchor(PathOf(AppStateDriver.FixturePath), $"{AppStateDriver.FixturesNamespace}.{name}");
    }

    /// <summary>Where the fixture type <paramref name="name"/> is, as this build saves it.</summary>
    private static NodePath FixturePlace(string name)
    {
        return NodeDefinitions.CreatePath(PathOf(AppStateDriver.FixturePath),
        [
            new NodePathStep(new NodePathSegment.Namespace(AppStateDriver.FixturesNamespace), AppStateDriver.FixturesNamespace),
            new NodePathStep(new NodePathSegment.Definition($"T:{AppStateDriver.FixturesNamespace}.{name}"), name),
        ]);
    }

    [Fact]
    public void Scrolling_alone_writes_no_settings_and_the_next_save_keeps_where_the_view_stands()
    {
        var app = new AppDriver(height: 360f, settings: _temp.Settings());
        app.Driver.OpenFixtures();
        app.Run(3);
        app.Show(app.Fixture("Outline"), 4);
        var outline = app.AppState.Tabs.Active;
        Assert.NotNull(outline);
        app.Run(3);
        var file = _temp.PathOf("settings.json");
        var before = File.ReadAllText(file);

        var position = ScrollTo(app, outline, 20, new TextPosition(21, 4), new TextPosition(22, 8));
        app.Run(3);
        Assert.Equal(before, File.ReadAllText(file));

        app.App.SaveOnExit(new WindowBounds(10, 20, 1000, 360, false));
        var next = new AppDriver(height: 360f, settings: _temp.Settings());
        next.Start(RestoreActiveAssemblyListAsync);
        next.Settle(4);
        Assert.Equal(position, CodeOf(next, TabTitled(next, "Outline")).Position);
    }

    [Fact]
    public void A_tab_never_shown_after_a_restart_keeps_where_its_view_was_through_the_next_save()
    {
        var app = new AppDriver(height: 360f, settings: _temp.Settings());
        app.Driver.OpenFixtures();
        app.Run(3);
        app.Show(app.Fixture("Outline"), 4);
        var outline = app.AppState.Tabs.Active;
        Assert.NotNull(outline);
        app.AppState.Navigation.KeepTabOpen(outline);
        var outlinePosition = ScrollTo(app, outline, 20, new TextPosition(21, 4), new TextPosition(22, 8));
        app.Show(app.Fixture("Greeter"), 4, inNewTab: true);
        app.Run(3);
        app.App.SaveOnExit(new WindowBounds(10, 20, 1000, 360, false));

        var hidden = new AppDriver(height: 360f, settings: _temp.Settings());
        hidden.Start(RestoreActiveAssemblyListAsync);
        hidden.Settle(4);
        hidden.App.SaveOnExit(new WindowBounds(10, 20, 1000, 360, false));

        var next = new AppDriver(height: 360f, settings: _temp.Settings());
        next.Start(RestoreActiveAssemblyListAsync);
        next.Settle(4);
        ActivateTab(next.Ui, next.AppState.Navigation, next.AppState.Language, next.AppState.DecompilerOptions, TabTitled(next, "Outline"));
        next.Run(4);

        Assert.Equal(outlinePosition, CodeOf(next, TabTitled(next, "Outline")).Position);
    }

    [Fact]
    public void After_a_restart_the_tabs_come_back_in_their_panels_and_groups_with_their_views_where_they_were()
    {
        var app = new AppDriver(height: 360f, settings: _temp.Settings());
        app.Driver.OpenFixtures();
        app.Run(3);
        app.Show(app.Fixture("Outline"), 4);
        var outline = app.AppState.Tabs.Active;
        Assert.NotNull(outline);
        app.AppState.Navigation.KeepTabOpen(outline);
        var outlinePosition = ScrollTo(app, outline, 20, new TextPosition(21, 4), new TextPosition(22, 8));
        app.Show(app.Fixture("Greeter"), 4, inNewTab: true);
        var greeter = app.AppState.Tabs.Active;
        Assert.NotNull(greeter);
        MoveRightOf(app, greeter, outline);
        app.Show(app.Fixture("Farewell"), 4);
        DiagramView.ShowInNewDiagramLater(app.Ui, app.Fixture("Vehicle").Definition, Names.Diagram(app.Fixture("Vehicle").Definition));
        app.Settle(2);
        var diagram = app.AppState.Tabs.Active;
        Assert.NotNull(diagram);
        SettleDiagram(app, diagram);
        var view = DiagramOf(app, diagram);
        DiagramCameras.Restore(view.Camera, new PointF(-30f, 12f), 1.5f);
        Assert.NotNull(diagram.Diagram);
        var fields = new CardFold.Own(diagram.Diagram.Cards[0].Anchor, MemberSection.Fields);
        Diagrams.ToggleFold(view, fields);
        Diagrams.ShowHiddenMembers(view, true);
        ActivateTab(app.Ui, app.AppState.Navigation, app.AppState.Language, app.AppState.DecompilerOptions, greeter);
        app.Run(3);
        app.App.SaveOnExit(new WindowBounds(10, 20, 1000, 360, false));

        var next = new AppDriver(height: 360f, settings: _temp.Settings());
        next.Start(RestoreActiveAssemblyListAsync);
        next.Settle(4);

        Assert.Equal(ListTabPanels(app), ListTabPanels(next));
        Assert.Equal(ListGroups(app), ListGroups(next));
        Assert.Equal("Greeter", next.AppState.Tabs.Active?.Title);
        Assert.Equal("Farewell", next.AppState.Tabs.Preview?.Title);
        Assert.Equal("Farewell", TabTitled(next, "Farewell").Node?.Text);
        Assert.Equal(outlinePosition, CodeOf(next, TabTitled(next, "Outline")).Position);
        var diagramTitle = diagram.Title;
        Assert.NotNull(diagramTitle);
        var restored = TabTitled(next, diagramTitle);
        var restoredView = DiagramOf(next, restored);
        Assert.Equal(ListTypes(diagram), ListTypes(restored));
        Assert.Equal(new PointF(-30f, 12f), restoredView.Camera.Corner);
        Assert.Equal(1.5f, restoredView.Camera.Zoom);
        Assert.False(Diagrams.IsFoldOpen(restoredView, fields));
        Assert.True(restoredView.ShowsHiddenMembers);
    }

    [Fact]
    public void A_grid_never_shown_after_a_restart_keeps_the_row_it_was_left_on_through_the_next_save()
    {
        var app = new AppDriver(height: 360f, settings: _temp.Settings());
        app.Driver.OpenFixtures();
        app.Run(3);
        app.Show(app.Node("Metadata", "Tables").Table("TypeDef"), 4);
        var tab = GridTab(app);
        app.AppState.Navigation.KeepTabOpen(tab);
        var grid = tab.Grid;
        Assert.NotNull(grid);
        var row = grid.RowNamed("Greeter");
        MetadataGridWidget.RevealRow(GridOf(app, tab), row);
        app.Run(3);
        app.Show(app.Fixture("Greeter"), 4, inNewTab: true);
        app.Run(3);
        app.App.SaveOnExit(new WindowBounds(10, 20, 1000, 360, false));

        var hidden = new AppDriver(height: 360f, settings: _temp.Settings());
        hidden.Start(RestoreActiveAssemblyListAsync);
        hidden.Settle(4);
        hidden.App.SaveOnExit(new WindowBounds(10, 20, 1000, 360, false));

        var next = new AppDriver(height: 360f, settings: _temp.Settings());
        next.Start(RestoreActiveAssemblyListAsync);
        next.Settle(4);
        ActivateTab(next.Ui, next.AppState.Navigation, next.AppState.Language, next.AppState.DecompilerOptions, GridTab(next));
        next.Run(4);

        Assert.Equal(row, GridOf(next, GridTab(next)).Selected);
    }

    [Fact]
    public void A_grid_comes_back_after_a_restart_on_the_row_it_was_left_on()
    {
        var row = SaveGridOnGreeter();

        Assert.Contains("\"GridPosition\"", File.ReadAllText(_temp.PathOf("settings.json")));
        var next = new AppDriver(height: 360f, settings: _temp.Settings());
        next.Start(RestoreActiveAssemblyListAsync);
        next.Settle(4);

        Assert.Equal(row, GridOf(next, GridTab(next)).Selected);
    }

    [Fact]
    public void A_grid_saved_with_a_code_views_position_by_an_older_build_comes_back_at_the_top()
    {
        var row = SaveGridOnGreeter();
        var settings = _temp.Settings();
        var documents = Assert.Single(settings.AssemblyLists).Documents;
        Assert.NotNull(documents);
        var saved = documents.Tabs.OfType<NodeDocumentRecord>().Single(t => t.View?.Grid is not null);
        var older = saved with { View = ViewPosition.OfCode(new CodeViewPosition(row, 0f, default, new TextPosition(row, 1))) };
        documents.Tabs[documents.Tabs.IndexOf(saved)] = older;

        var next = new AppDriver(height: 360f, settings: settings);
        next.Start(RestoreActiveAssemblyListAsync);
        next.Settle(4);

        Assert.Equal(MetadataGridState.None, GridOf(next, GridTab(next)).Selected);
    }

    [Fact]
    public void A_tab_whose_node_is_gone_after_a_restart_says_so_in_its_panel()
    {
        var missing = new NodeDocumentRecord(MissingPanel, "Missing", FixturePlace("Missing"), DecompilationLanguage.CSharp, null);
        var d = new AppStateDriver(SettingsWith(new DocumentsRecord([missing], MissingPanel, null, null)));

        d.Start(RestoreActiveAssemblyListAsync);
        d.Settle();

        var tab = Assert.Single(d.AppState.Tabs.Open);
        var gone = Assert.IsType<TabContent.Gone>(tab.Content);
        Assert.Equal("Missing", gone.Location.Label);
        Assert.Equal(MissingPanel, DocumentPanels.GetPanel(d.Ui.Documents, tab));
        Assert.True(d.Ui.Layout.Contains(MissingPanel));
    }

    [Fact]
    public void Clearing_the_list_before_it_reopens_settles_its_tabs_rather_than_leaving_them_reloading()
    {
        var greeter = new NodeDocumentRecord(MissingPanel, "Greeter", FixturePlace("Greeter"), DecompilationLanguage.CSharp, null);
        var d = new AppStateDriver(SettingsWith(new DocumentsRecord([greeter], MissingPanel, null, null)));

        d.Start(RestoreActiveAssemblyListAsync);
        d.Change(CloseAllAssemblies);
        d.Settle();

        var tab = Assert.Single(d.AppState.Tabs.Open);
        Assert.IsType<TabContent.Gone>(tab.Content);
    }

    [Fact]
    public void A_diagram_comes_back_without_the_types_no_longer_found_and_is_the_one_shown_types_go_to()
    {
        var vehicle = FixtureType("Vehicle");
        var saved = new DiagramDocumentRecord(DiagramPanel, "Vehicle diagram", [vehicle, FixtureType("Missing")], [PathOf(AppStateDriver.FixturePath)], null);
        var d = new AppStateDriver(SettingsWith(new DocumentsRecord([saved], DiagramPanel, null, null)));

        d.Start(RestoreActiveAssemblyListAsync);
        d.Settle();

        var tab = Assert.Single(d.AppState.Tabs.Open);
        Assert.Equal("Vehicle diagram", tab.Title);
        Assert.Equal([vehicle], ListTypes(tab));
        var diagram = tab.Diagram;
        Assert.NotNull(diagram);
        Assert.True(Diagrams.IsHome(diagram, PathOf(AppStateDriver.FixturePath)));
        Assert.Same(tab, d.AppState.Tabs.Diagram);
    }

    [Fact]
    public void Switching_lists_swaps_their_documents()
    {
        var d = AppStateDriver.Opened();
        d.Show(d.Fixture("Greeter"));
        d.AppState.Lists.Add(Other);
        d.Start((a, ct) => SwitchAssemblyListAsync(a, Other, ct));
        d.Settle();
        d.Start((a, ct) => a.AssemblyOpening.OpenAssemblyAsync(a, AppStateDriver.FixturePath, ct));
        d.RunUntil(() => AssemblyListing.ListFilePaths(d.AppState.Assemblies).Contains(PathOf(AppStateDriver.FixturePath)));
        d.Settle();
        d.Show(d.Fixture("Farewell"));

        d.Start((a, ct) => SwitchAssemblyListAsync(a, SavedAssemblyLists.DefaultName, ct));
        d.Settle();
        var first = Assert.Single(d.AppState.Tabs.Open);
        var firstPanel = DocumentPanels.GetPanel(d.Ui.Documents, first);
        var firstDocuments = Docking.ListDocumentPanels(d.Ui.MainLayout);
        d.Start((a, ct) => SwitchAssemblyListAsync(a, Other, ct));
        d.Settle();
        var other = Assert.Single(d.AppState.Tabs.Open);

        Assert.Equal("Greeter", first.Node?.Text);
        Assert.Equal([firstPanel], firstDocuments);
        Assert.Equal("Farewell", other.Node?.Text);
        Assert.Equal([DocumentPanels.GetPanel(d.Ui.Documents, other)], Docking.ListDocumentPanels(d.Ui.MainLayout));
    }

    [Fact]
    public void Switching_back_to_a_list_docks_its_documents_side_by_side_as_they_were()
    {
        var d = AppStateDriver.Opened();
        d.Show(d.Fixture("Greeter"));
        var greeter = d.AppState.Tabs.Active;
        Assert.NotNull(greeter);
        d.AppState.Navigation.KeepTabOpen(greeter);
        var greeterPanel = DocumentPanels.GetPanel(d.Ui.Documents, greeter);
        d.Show(d.Fixture("Farewell"), inNewTab: true);
        var layout = d.Ui.Layout;
        var farewell = d.AppState.Tabs.Active;
        Assert.NotNull(farewell);
        var greeterGroup = layout.GroupOf(greeterPanel);
        Assert.NotNull(greeterGroup);
        layout.Dock(DocumentPanels.GetPanel(d.Ui.Documents, farewell), greeterGroup, DockEdge.Right);
        d.Settle();
        d.AppState.Lists.Add(Other);
        d.Start((a, ct) => SwitchAssemblyListAsync(a, Other, ct));
        d.Settle();

        d.Start((a, ct) => SwitchAssemblyListAsync(a, SavedAssemblyLists.DefaultName, ct));
        d.Settle();

        var tabs = d.AppState.Tabs.Open.ToList();
        Assert.Equal(["Greeter", "Farewell"], tabs.Select(static t => t.Title));
        var left = layout.GroupOf(DocumentPanels.GetPanel(d.Ui.Documents, tabs[0]));
        Assert.NotNull(left);
        var right = layout.GroupOf(DocumentPanels.GetPanel(d.Ui.Documents, tabs[1]));
        Assert.NotNull(right);
        var split = ParentOf(layout.Root, left);
        Assert.NotNull(split);
        Assert.Equal(Direction.Horizontal, split.Direction);
        Assert.Same(left, split.First);
        Assert.Same(right, split.Second);
        Assert.Equal("Farewell", d.AppState.Tabs.Active?.Title);
    }

    private static DockSplit? ParentOf(DockNode node, DockNode target)
    {
        return node switch
        {
            DockSplit split when split.First == target || split.Second == target => split,
            DockSplit split => ParentOf(split.First, target) ?? ParentOf(split.Second, target),
            _ => null,
        };
    }

    [Fact]
    public void A_hand_edited_list_keeps_one_document_per_panel_and_none_docked_as_a_view()
    {
        var settings = _temp.Settings();
        List<DocumentRecord> tabs = [HandTab("document-1", "First"), HandTab("document-1", "Twin"), HandTab(DockPanels.Assemblies, "Tree")];
        settings.AssemblyLists.Add(new AssemblyListRecord("Hand", [], Documents: new DocumentsRecord(tabs, null, null, null)));
        TempSettings.Save(settings);

        var documents = _temp.Settings().AssemblyLists.Single(static l => l.Name == "Hand").Documents;
        Assert.NotNull(documents);
        var kept = documents.Tabs;

        Assert.Equal(["First"], kept.Select(static t => t.Title));
    }

    private static NodeDocumentRecord HandTab(string panel, string title)
    {
        return new NodeDocumentRecord(panel, title, NodeDefinitions.CreatePath(PathOf(AppStateDriver.FixturePath), []), DecompilationLanguage.CSharp, null);
    }

    [Fact]
    public void A_new_panel_skips_every_panel_saved_documents_were_docked_as_however_numbered()
    {
        var documents = new DocumentViews();

        DocumentPanels.SkipPanels(documents, ["document-1", "document-2", "document-2147483647"]);

        Assert.Equal("document-3", DocumentPanels.CreatePanel(documents));
    }

    [Fact]
    public void Tabs_showing_nothing_are_not_kept_and_the_next_run_starts_with_one_empty_tab_as_ever()
    {
        var d = new AppStateDriver(_temp.Settings());
        d.NextFrame();
        d.AppState.SettingsSaving.Save(d.AppState);

        var saved = _temp.Settings();
        var next = new AppStateDriver(saved);

        Assert.Null(Assert.Single(saved.AssemblyLists).Documents);
        var tab = Assert.Single(next.AppState.Tabs.Open);
        Assert.Null(tab.Location);
        Assert.True(next.Ui.Layout.Contains(DocumentPanels.GetPanel(next.Ui.Documents, tab)));
    }

    [Fact]
    public void A_tab_opened_is_saved_with_the_next_frame()
    {
        var app = new AppDriver(settings: _temp.Settings());
        app.Driver.OpenFixtures();
        app.Run(2);

        app.Show(app.Fixture("Greeter"), 1, inNewTab: true);

        var documents = Assert.Single(_temp.Settings().AssemblyLists).Documents;
        Assert.NotNull(documents);
        Assert.Contains(documents.Tabs, static t => t.Title == "Greeter");
    }
}
