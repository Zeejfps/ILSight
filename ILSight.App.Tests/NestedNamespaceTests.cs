using ILSight.Core;
using ILSight.CommandArea;
using static ILSight.BookmarkArea.Bookmarks;
using static ILSight.CodeDisplay;
using ILSight.BookmarkArea;

namespace ILSight.App.Tests;

public sealed class NestedNamespaceTests
{
    private static readonly string[] EnclosingNamespaces = ["ILSight", "ILSight.App", "ILSight.App.Tests"];

    private static AppDriver OpenedNested()
    {
        var app = AppDriver.Opened();
        Nest(app, true);
        return app;
    }

    private static void Nest(AppDriver app, bool nest)
    {
        SetDisplayOptions(app.Ui, app.Ui.Display with { NestedNamespaces = nest });
        app.Run();
    }

    /// <summary>Whether the tree shows <paramref name="node"/> down the nested namespaces: each one
    /// expanded and listing the next, and the fixtures' namespace listing the node.</summary>
    private static bool IsShownNested(AppDriver app, BrowseNode node)
    {
        var tree = app.Ui.Tree;
        var parent = app.Node();
        foreach (var name in EnclosingNamespaces.Append(AppStateDriver.FixturesNamespace))
        {
            var ns = app.Node(name);
            if (!tree.View.IsExpanded(parent))
            {
                return false;
            }

            var shown = ShownChildren(app, parent);
            if (!shown.Contains(ns))
            {
                return false;
            }

            parent = ns;
        }
        if (!tree.View.IsExpanded(parent))
        {
            return false;
        }

        return ShownChildren(app, parent).Contains(node);
    }

    /// <summary>The children the tree shows under <paramref name="node"/>, built first rather than
    /// shown loading.</summary>
    private static IReadOnlyList<BrowseNode> ShownChildren(AppDriver app, BrowseNode node)
    {
        node.EnsureChildren();
        return app.Ui.Tree.GetChildren(node).Nodes;
    }

    [Fact]
    public void Each_namespace_shows_under_the_one_its_name_extends_by_its_last_segment()
    {
        var app = OpenedNested();
        var tree = app.Ui.Tree;
        var ilsight = app.Node("ILSight");
        var fixtures = app.Node(AppStateDriver.FixturesNamespace);

        Commands.StartExpandingNamespaces(app.AppState, tree, Trees.ExpandNamespaces(tree));
        app.Frames.WaitUntil(() => tree.View.IsExpanded(fixtures), Patience);

        Assert.All(EnclosingNamespaces, name => Assert.True(tree.View.IsExpanded(app.Node(name))));
        Assert.True(tree.View.IsExpanded(fixtures));
        Assert.Equal([app.Node("ILSight.App"), app.Node("ILSight.Tests")], ShownChildren(app, ilsight));
        Assert.True(tree.HasChildren(ilsight));
        Assert.Contains(fixtures, ShownChildren(app, app.Node("ILSight.App.Tests")));
        Assert.Equal("ILSight", Trees.GetRowLabel(tree, ilsight));
        Assert.Equal("App", Trees.GetRowLabel(tree, app.Node("ILSight.App")));
        Assert.Equal("Fixtures", Trees.GetRowLabel(tree, fixtures));
        Assert.Equal(AppStateDriver.FixturesNamespace, Trees.GetLabel(tree, fixtures));
        Assert.True(IsShownNested(app, app.Fixture("Greeter")));
    }

    [Fact]
    public void Showing_public_api_a_namespace_of_internal_types_is_hidden_unless_a_namespace_nested_in_it_shows()
    {
        var tree = new BrowseTree { Session = TestSession.Shared };
        var root = new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath).Root;
        var fixtures = root.Child(AppStateDriver.FixturesNamespace);
        var vault = root.Child(AppStateDriver.FixturesNamespace + ".Vault");
        var door = root.Child(AppStateDriver.FixturesNamespace + ".Vault.Door");
        fixtures.EnsureChildren();
        vault.EnsureChildren();
        Trees.ShowApi(tree, ApiVisibility.PublicOnly);

        Assert.DoesNotContain(vault, tree.GetChildren(root).Nodes);
        Assert.Contains(door, tree.GetChildren(root).Nodes);

        Trees.NestNamespaces(tree, true);
        Assert.Contains(vault, tree.GetChildren(fixtures).Nodes);
        Assert.Equal([door], tree.GetChildren(vault).Nodes);

        Trees.NestNamespaces(tree, false);
        Trees.ShowApi(tree, ApiVisibility.AllButCompilerGenerated);
        Assert.Contains(vault, tree.GetChildren(root).Nodes);
    }

    [Fact]
    public void Switching_the_tree_to_nested_namespaces_keeps_the_selected_node_in_view()
    {
        var app = AppDriver.Opened();
        var greeter = app.Fixture("Greeter");
        app.Show(greeter, 2);

        Nest(app, true);

        Assert.Same(greeter, app.Ui.Tree.View.Selected);
        Assert.True(IsShownNested(app, greeter));
    }

    [Fact]
    public void Following_a_reference_reveals_the_type_under_its_nested_namespaces()
    {
        var app = OpenedNested();
        var farewell = app.Fixture("Farewell");
        Trees.CollapseTree(app.Ui.Tree);

        app.Start((a, ct) => a.NavigateHandler.HandleAsync(a, farewell.Definition, a.Navigation.BeginResolving(inNewTab: false), ct));
        app.Settle(1);

        Assert.Same(farewell, app.AppState.Navigation.ActiveTab?.Node);
        Assert.Same(farewell, app.Ui.Tree.View.Selected);
        Assert.True(IsShownNested(app, farewell));
    }

    [Fact]
    public void Going_back_in_nested_namespaces_to_a_place_shown_with_flat_ones_reveals_it_nested()
    {
        var app = AppDriver.Opened();
        var greeter = app.Fixture("Greeter");
        app.Show(greeter, 2);
        app.Show(app.Fixture("Farewell"), 2);
        Nest(app, true);
        Trees.CollapseTree(app.Ui.Tree);

        app.GoBack();
        app.Settle(1);

        Assert.Same(greeter, app.AppState.Navigation.ActiveTab?.Node);
        Assert.Same(greeter, app.Ui.Tree.View.Selected);
        Assert.True(IsShownNested(app, greeter));
    }

    [Fact]
    public void A_bookmark_made_with_nested_namespaces_names_the_namespace_whole_and_opens_with_flat_ones()
    {
        var app = OpenedNested();
        var outline = app.Fixture("Outline");
        app.Show(outline, 2);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.IsType<BookmarkToggle.Added>(ToggleBookmark(app.AppState, tab, tab.Document.GetPosition("return First();").Line, Names.Bookmark));
        var bookmark = Assert.Single(app.AppState.Bookmarks.Items);
        app.Show(app.Fixture("Greeter"), 2);
        Nest(app, false);
        Trees.CollapseTree(app.Ui.Tree);

        app.Start((a, ct) => a.GoToBookmarkHandler.HandleAsync(a, bookmark, ct));
        app.Settle(1);

        var fixtures = app.Node(AppStateDriver.FixturesNamespace);
        Assert.Equal([new NodePathSegment.Namespace(AppStateDriver.FixturesNamespace), new NodePathSegment.Definition($"T:{AppStateDriver.FixturesNamespace}.Outline")],
            bookmark.Place.Steps.Select(static s => s.Segment));
        Assert.Same(outline, app.AppState.Navigation.ActiveTab?.Node);
        Assert.Same(outline, app.Ui.Tree.View.Selected);
        Assert.True(app.Ui.Tree.View.IsExpanded(fixtures));
        Assert.Contains(fixtures, ShownChildren(app, app.Node()));
    }

    [Fact]
    public void Reloading_with_nested_namespaces_keeps_them_expanded_and_the_tab_revealed()
    {
        var app = OpenedNested();
        var before = app.Node("ILSight.App");
        app.Show(app.Fixture("Greeter"), 2);

        app.Start((a, ct) => a.AssemblyReloading.RefreshAssembliesAsync(a.Lists, a.ListSession, a.ViewCommands, a.Decompiler, Trees.ListExpandedPaths(app.Ui.Tree), ct));
        app.Settle(1);

        var greeter = app.Fixture("Greeter");
        Assert.NotSame(before, app.Node("ILSight.App"));
        Assert.Same(greeter, app.AppState.Navigation.ActiveTab?.Node);
        Assert.Same(greeter, app.Ui.Tree.View.Selected);
        Assert.True(IsShownNested(app, greeter));
    }

    [Fact]
    public void A_namespace_that_only_encloses_others_selects_its_assembly_once_namespaces_are_flat()
    {
        var app = OpenedNested();
        var enclosing = app.Node("ILSight.App");
        app.Show(enclosing, 2);
        Assert.Same(enclosing, app.Ui.Tree.View.Selected);

        Nest(app, false);

        Assert.Same(app.Node(), app.Ui.Tree.View.Selected);
        Assert.DoesNotContain(enclosing, ShownChildren(app, app.Node()));
        Assert.Same(enclosing, app.AppState.Navigation.ActiveTab?.Node);
    }
}
