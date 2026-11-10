using ZGF.Gui.Immediate;
using ILSight.Core;
using static ILSight.Assemblies;
using static ILSight.Navigation;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

public sealed class AssemblyMenuTests
{
    private static readonly string CoreLibPath = typeof(object).Assembly.Location;

    [Fact]
    public void The_assemblies_panel_sorts_the_list_by_name_and_closes_the_files_that_failed_to_load()
    {
        using var temp = new TempDir();
        var missing = temp.PathOf("Missing.dll");
        var app = AppDriver.Opened();
        OpenAssemblies(app.AppState, [PathOf(missing), PathOf(CoreLibPath)]);
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 3, Patience);
        app.Settle(2);

        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/sort-list")).Frame(2);

        var sorted = AssemblyListing.ListFilePaths(app.AppState.Assemblies);
        Assert.Equal([AppStateDriver.FixturePath, missing, CoreLibPath], sorted.Select(static p => p.Text));

        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/close-failed")).Frame(2);

        var roots = app.Ui.Tree.Roots;
        Assert.Equal([AppStateDriver.FixturePath, CoreLibPath], roots.Select(r => r.Assembly.FilePath.Text));
        Assert.False(AssemblyListing.AnyFailedToLoad(app.AppState.Assemblies));
    }

    [Fact]
    public void The_tree_menu_shows_an_assembly_selected_in_its_folder()
    {
        var app = AppDriver.Opened();
        app.Run(2);
        var assembly = app.AppState.Assemblies.Assemblies[0];

        app.PickFromTreeMenu(assembly.Name, "Open containing folder");

        Assert.Equal([assembly.FilePath.Text], app.Frames.RevealedFiles);
    }

    [Fact]
    public void The_tree_menu_opens_the_api_reference_page_of_a_public_type_and_offers_none_for_an_internal_one()
    {
        var app = AppDriver.Opened();
        var greeter = app.Fixture("Greeter");
        var hidden = app.Fixture("Hidden");
        Trees.RevealNode(app.Ui.Tree, hidden);
        Trees.RevealNode(app.Ui.Tree, greeter);
        app.Run(2);

        app.PickFromTreeMenu("Greeter", "Show in .NET API reference");
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.TreeItem, "Hidden"), PointerButton.Right).Frame();

        Assert.Equal([MicrosoftDocs.FindPage(greeter)!], app.Frames.OpenedUrls);
        Assert.EndsWith("/ilsight.app.tests.fixtures.greeter", app.Frames.OpenedUrls[0]);
        Assert.True(app.Frames.Drew("Copy full name"));
        Assert.False(app.Frames.Drew("Show in .NET API reference"));
    }

    [Fact]
    public void A_reference_in_code_opens_its_api_reference_page_and_a_private_one_reports_it_has_none()
    {
        var app = AppDriver.Opened();
        app.Show(app.Fixture("Greeter"), 2);
        app.Settle(2);
        var document = app.AppState.Tabs.Active!.Document;
        var references = document.SpansOf()
            .Select(s => s.Span.Reference)
            .OfType<CodeReference.Entity>()
            .Where(r => r.Kind == EntityKind.Type)
            .ToList();
        var greeter = references.First(r => r.Name == "Greeter");
        var internalType = app.Fixture("Hidden").Definition;

        ShowInApiReference(app.AppState, greeter);
        app.Frames.WaitUntil(() => app.Frames.OpenedUrls.Count == 1, Patience);
        ShowInApiReference(app.AppState, internalType);
        app.Frames.WaitUntil(() => app.AppState.Errors.Entries.Count == 1, Patience);

        Assert.EndsWith("/ilsight.app.tests.fixtures.greeter", app.Frames.OpenedUrls[0]);
        Assert.StartsWith("Hidden has no page in the .NET API reference", app.AppState.Errors.Entries[0].Words().FullText);
    }

    [Fact]
    public void The_tree_menu_loads_the_dependencies_of_an_assembly()
    {
        var app = AppDriver.Opened();
        app.Run(2);
        var name = app.AppState.Assemblies.Assemblies[0].Name;

        app.Frames.ClickOn(Selector.OfRole(SemanticRole.TreeItem, name), PointerButton.Right).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Load dependencies")).Frame();
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Assemblies.Any(a => a.Name == "System.Runtime"), Patience);
        app.Settle(2);

        var names = app.Ui.Tree.Roots.Select(r => r.Assembly.Name).ToList();
        Assert.Equal(name, names[0]);
        Assert.Contains("System.Runtime", names);
        Assert.Contains("ILSight.Core", names);
    }
}
