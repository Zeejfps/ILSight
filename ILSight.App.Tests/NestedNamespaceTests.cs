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
    private static async Task<bool> IsShownNestedAsync(AppDriver app, BrowseNode node)
    {
        var tree = app.Ui.Tree;
        var parent = await app.NodeAsync();
        foreach (var name in EnclosingNamespaces.Append(AppStateDriver.FixturesNamespace))
        {
            var ns = await app.NodeAsync(name);
            if (!tree.View.IsExpanded(parent))
            {
                return false;
            }

            var shown = await ShownChildrenAsync(app, parent);
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

        return (await ShownChildrenAsync(app, parent)).Contains(node);
    }

    /// <summary>The children the tree shows under <paramref name="node"/>, built first rather than
    /// shown loading.</summary>
    private static async Task<IReadOnlyList<BrowseNode>> ShownChildrenAsync(AppDriver app, BrowseNode node)
    {
        await node.EnsureChildrenAsync();
        return app.Ui.Tree.GetChildren(node).Nodes;
    }

    [Fact]
    public async Task Each_namespace_shows_under_the_one_its_name_extends_by_its_last_segment()
    {
        var app = OpenedNested();
        var tree = app.Ui.Tree;
        var ilsight = await app.NodeAsync("ILSight");
        var fixtures = await app.NodeAsync(AppStateDriver.FixturesNamespace);

        app.Driver.InTurn(() => Commands.StartExpandingNamespaces(app.AppState, tree, tree.ExpandNamespaces()));
        app.Frames.WaitUntil(() => tree.View.IsExpanded(fixtures), Patience);

        foreach (var name in EnclosingNamespaces)
        {
            Assert.True(tree.View.IsExpanded(await app.NodeAsync(name)));
        }
        Assert.True(tree.View.IsExpanded(fixtures));
        Assert.Equal([await app.NodeAsync("ILSight.App"), await app.NodeAsync("ILSight.Tests")], await ShownChildrenAsync(app, ilsight));
        Assert.True(tree.HasChildren(ilsight));
        Assert.Contains(fixtures, await ShownChildrenAsync(app, await app.NodeAsync("ILSight.App.Tests")));
        Assert.Equal("ILSight", tree.GetRowLabel(ilsight));
        Assert.Equal("App", tree.GetRowLabel(await app.NodeAsync("ILSight.App")));
        Assert.Equal("Fixtures", tree.GetRowLabel(fixtures));
        Assert.Equal(AppStateDriver.FixturesNamespace, tree.GetLabel(fixtures));
        Assert.True(await IsShownNestedAsync(app, await app.FixtureAsync("Greeter")));
    }

    [Fact]
    public async Task Showing_public_api_a_namespace_of_internal_types_is_hidden_unless_a_namespace_nested_in_it_shows()
    {
        var tree = new BrowseTree { Session = TestSession.Shared };
        var root = await new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath).ReadRootAsync();
        var fixtures = await root.GetChildAsync(AppStateDriver.FixturesNamespace);
        var vault = await root.GetChildAsync(AppStateDriver.FixturesNamespace + ".Vault");
        var door = await root.GetChildAsync(AppStateDriver.FixturesNamespace + ".Vault.Door");
        await fixtures.EnsureChildrenAsync();
        await vault.EnsureChildrenAsync();
        tree.ShowApi(ApiVisibility.PublicOnly);

        Assert.DoesNotContain(vault, tree.GetChildren(root).Nodes);
        Assert.Contains(door, tree.GetChildren(root).Nodes);

        tree.NestNamespaces(true);
        Assert.Contains(vault, tree.GetChildren(fixtures).Nodes);
        Assert.Equal([door], tree.GetChildren(vault).Nodes);

        tree.NestNamespaces(false);
        tree.ShowApi(ApiVisibility.AllButCompilerGenerated);
        Assert.Contains(vault, tree.GetChildren(root).Nodes);
    }

    [Fact]
    public async Task Switching_the_tree_to_nested_namespaces_keeps_the_selected_node_in_view()
    {
        var app = AppDriver.Opened();
        var greeter = await app.FixtureAsync("Greeter");
        app.Show(greeter, 2);

        Nest(app, true);

        Assert.Same(greeter, app.Ui.Tree.View.Selected);
        Assert.True(await IsShownNestedAsync(app, greeter));
    }

    [Fact]
    public async Task Following_a_reference_reveals_the_type_under_its_nested_namespaces()
    {
        var app = OpenedNested();
        var farewell = await app.FixtureAsync("Farewell");
        app.Ui.Tree.CollapseAll();

        app.Start((a, ct) => a.NavigateHandler.HandleAsync(a.Navigation, a.Decompiler, farewell.Definition, a.Navigation.BeginResolving(inNewTab: false), ct));
        app.Settle(1);

        Assert.Same(farewell, app.AppState.Navigation.ActiveTab?.Node);
        Assert.Same(farewell, app.Ui.Tree.View.Selected);
        Assert.True(await IsShownNestedAsync(app, farewell));
    }

    [Fact]
    public async Task Going_back_in_nested_namespaces_to_a_place_shown_with_flat_ones_reveals_it_nested()
    {
        var app = AppDriver.Opened();
        var greeter = await app.FixtureAsync("Greeter");
        app.Show(greeter, 2);
        app.Show(await app.FixtureAsync("Farewell"), 2);
        Nest(app, true);
        app.Ui.Tree.CollapseAll();

        app.GoBack();
        app.Settle(1);

        Assert.Same(greeter, app.AppState.Navigation.ActiveTab?.Node);
        Assert.Same(greeter, app.Ui.Tree.View.Selected);
        Assert.True(await IsShownNestedAsync(app, greeter));
    }

    [Fact]
    public async Task A_bookmark_made_with_nested_namespaces_names_the_namespace_whole_and_opens_with_flat_ones()
    {
        var app = OpenedNested();
        var outline = await app.FixtureAsync("Outline");
        app.Show(outline, 2);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.IsType<BookmarkToggle.Added>(ToggleBookmark(app.AppState, tab, tab.Document.GetPosition("return First();").Line, Names.Bookmark));
        var bookmark = Assert.Single(app.AppState.Bookmarks.Items);
        app.Show(await app.FixtureAsync("Greeter"), 2);
        Nest(app, false);
        app.Ui.Tree.CollapseAll();

        app.Start((a, ct) => a.GoToBookmarkHandler.HandleAsync(a, bookmark, ct));
        app.Settle(1);

        var fixtures = await app.NodeAsync(AppStateDriver.FixturesNamespace);
        Assert.Equal([new NodePathSegment.Namespace(AppStateDriver.FixturesNamespace), new NodePathSegment.Definition($"T:{AppStateDriver.FixturesNamespace}.Outline")],
            bookmark.Place.Steps.Select(static s => s.Segment));
        Assert.Same(outline, app.AppState.Navigation.ActiveTab?.Node);
        Assert.Same(outline, app.Ui.Tree.View.Selected);
        Assert.True(app.Ui.Tree.View.IsExpanded(fixtures));
        Assert.Contains(fixtures, await ShownChildrenAsync(app, await app.NodeAsync()));
    }

    [Fact]
    public async Task Reloading_with_nested_namespaces_keeps_them_expanded_and_the_tab_revealed()
    {
        var app = OpenedNested();
        var before = await app.NodeAsync("ILSight.App");
        app.Show(await app.FixtureAsync("Greeter"), 2);

        app.Start((a, ct) => a.AssemblyReloading.RefreshAssembliesAsync(a.Lists, a.ListSession, a.ViewCommands, a.Decompiler, app.Ui.Tree.ListExpandedPaths(), ct));
        app.Settle(1);

        var greeter = await app.FixtureAsync("Greeter");
        Assert.NotSame(before, await app.NodeAsync("ILSight.App"));
        Assert.Same(greeter, app.AppState.Navigation.ActiveTab?.Node);
        Assert.Same(greeter, app.Ui.Tree.View.Selected);
        Assert.True(await IsShownNestedAsync(app, greeter));
    }

    [Fact]
    public async Task A_namespace_that_only_encloses_others_selects_its_assembly_once_namespaces_are_flat()
    {
        var app = OpenedNested();
        var enclosing = await app.NodeAsync("ILSight.App");
        app.Show(enclosing, 2);
        Assert.Same(enclosing, app.Ui.Tree.View.Selected);

        Nest(app, false);

        Assert.Same(await app.NodeAsync(), app.Ui.Tree.View.Selected);
        Assert.DoesNotContain(enclosing, await ShownChildrenAsync(app, await app.NodeAsync()));
        Assert.Same(enclosing, app.AppState.Navigation.ActiveTab?.Node);
    }
}
