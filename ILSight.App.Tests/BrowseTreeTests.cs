using System.Runtime.CompilerServices;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CommandArea;

namespace ILSight.App.Tests;

public sealed class BrowseTreeTests
{
    [Fact]
    public async Task A_node_whose_children_are_not_built_shows_a_loading_row_until_they_are()
    {
        var d = new AppStateDriver();
        var tree = new BrowseTree { Session = TestSession.Shared };
        var root = await new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath).ReadRootAsync();

        var first = tree.GetChildren(root);

        Assert.Empty(first.Nodes);
        var loading = Assert.IsType<TreeRow>(first.PlaceholderRow);
        Assert.Equal("Loading…", loading.Label);
        Assert.True(loading.Spinning);
        Assert.True(tree.Builds.HasAsked);
        Commands.StartBuildingChildren(d.AppState, tree);
        d.RunUntil(() => tree.GetChildren(root).PlaceholderRow is null);
        Assert.Contains(tree.GetChildren(root).Nodes, c => c.Text == AppStateDriver.FixturesNamespace);
    }

    [Fact]
    public async Task Children_that_fail_to_build_show_a_row_saying_why()
    {
        using var temp = new TempDir();
        var d = new AppStateDriver();
        var tree = new BrowseTree { Session = TestSession.Shared };
        var root = await new AssemblyList { Runtime = TestRuntime.Installed }.Open(BrokenAssemblies.WriteUnreadableAvaloniaContainer(temp)).ReadRootAsync();
        var container = await root.GetChildAsync("Resources", BrokenAssemblies.AvaloniaContainer);

        tree.GetChildren(container);
        Commands.StartBuildingChildren(d.AppState, tree);
        d.RunUntil(() => container.ChildrenState is ChildrenState.Failed);

        var children = tree.GetChildren(container);
        var failed = Assert.IsType<TreeRow>(children.PlaceholderRow);
        Assert.Empty(children.Nodes);
        Assert.StartsWith("Error: ", failed.Label);
        Assert.Contains("unknown version 9", failed.Label);
        Assert.False(failed.Spinning);
        Assert.True(tree.HasChildren(container));
    }

    [Fact]
    public async Task A_module_whose_types_the_library_cannot_read_says_why_in_the_tree_and_nothing_in_the_error_log()
    {
        using var temp = new TempDir();
        var path = BrokenAssemblies.WriteUnreadableTypeSystem(temp, Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleApp", "SampleApp.dll"));
        var app = new AppDriver();
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(path), AssemblyPath.Of(AppStateDriver.FixturePath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Assemblies.Count == 2, AppStateDriver.Patience);
        app.Settle(2);
        var module = app.AppState.Assemblies.Assemblies[0];

        var root = await module.ReadRootAsync();
        app.Ui.Tree.View.SetExpanded(root, true);
        app.Ui.Tree.View.Refresh();
        app.Frames.WaitUntil(() => root.ChildrenState is ChildrenState.Failed, AppStateDriver.Patience);
        app.Settle(2);

        var failed = Assert.IsType<TreeRow>(app.Ui.Tree.GetChildren(root).PlaceholderRow);
        Assert.StartsWith("Error: ", failed.Label);
        Assert.Single(app.Frames.TextRects(), t => t.Text == failed.Label);
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public async Task An_assembly_spins_while_it_is_reopened()
    {
        var tree = new BrowseTree { Session = TestSession.Shared };
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath);
        var reopens = new Reopens();
        tree.ShowReopening(reopens.Files);
        var idle = tree.Row(await assembly.ReadRootAsync()).Spinning;

        reopens.Start(assembly.FilePath);

        Assert.False(idle);
        Assert.True(tree.Row(await assembly.ReadRootAsync()).Spinning);
        Assert.False(tree.Row(await assembly.ReadRootAsync().GetChildAsync(AppStateDriver.FixturesNamespace)).Spinning);
    }

    [Fact]
    public async Task Revealing_a_hidden_non_public_node_selects_its_nearest_shown_ancestor()
    {
        var d = new AppStateDriver();
        var tree = new BrowseTree { Session = TestSession.Shared };
        var root = await new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath).ReadRootAsync();
        var ns = await root.GetChildAsync(AppStateDriver.FixturesNamespace);
        var secret = await ns.GetChildAsync("Hidden", "Secret() : int");
        tree.ShowApi(ApiVisibility.PublicOnly);

        tree.RevealNode(secret);
        Assert.Same(ns, tree.View.Selected);

        tree.ShowApi(ApiVisibility.AllButCompilerGenerated);
        tree.RevealNode(secret);
        Assert.Same(secret, tree.View.Selected);
    }

    [Fact]
    public async Task Compiler_generated_members_show_only_when_everything_is_shown()
    {
        var tree = new BrowseTree { Session = TestSession.Shared };
        var root = await new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath).ReadRootAsync();
        var counter = await root.GetChildAsync(AppStateDriver.FixturesNamespace, "Counter");
        var backingField = await counter.GetChildAsync("<Count>k__BackingField : int");

        Assert.True(backingField.IsCompilerGenerated);
        Assert.DoesNotContain(backingField, tree.GetChildren(counter).Nodes);

        tree.ShowApi(ApiVisibility.All);

        Assert.Contains(backingField, tree.GetChildren(counter).Nodes);
    }

    [Fact]
    public async Task The_public_children_cache_does_not_keep_closed_assemblies_alive()
    {
        var tree = new BrowseTree { Session = TestSession.Shared };
        tree.ShowApi(ApiVisibility.PublicOnly);
        var assembly = await FilterAndCloseAsync(tree);

        for (var i = 0; i < 100 && assembly.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(20);
        }

        Assert.False(assembly.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> FilterAndCloseAsync(BrowseTree tree)
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var assembly = list.Open(AppStateDriver.FixturePath);
        var root = await assembly.ReadRootAsync();
        _ = await root.ListChildrenAsync();
        Assert.NotEmpty(tree.GetChildren(root).Nodes);
        LoadedAssemblies.Release(list.Close(assembly));
        return new WeakReference(assembly);
    }
}
