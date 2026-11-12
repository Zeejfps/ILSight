using System.Runtime.CompilerServices;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Commanding;

namespace ILSight.App.Tests;

public sealed class BrowseTreeTests
{
    [Fact]
    public void A_node_whose_children_are_not_built_shows_a_loading_row_until_they_are()
    {
        var d = new AppStateDriver();
        var tree = new BrowseTree { Session = TestSession.Shared };
        var root = new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath).Root;

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
    public void Children_that_fail_to_build_show_a_row_saying_why()
    {
        using var temp = new TempDir();
        var d = new AppStateDriver();
        var tree = new BrowseTree { Session = TestSession.Shared };
        var root = new AssemblyList { Runtime = TestRuntime.Installed }.Open(BrokenAssemblies.WriteUnreadableAvaloniaContainer(temp)).Root;
        var container = root.Child("Resources", BrokenAssemblies.AvaloniaContainer);

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
    public void An_assembly_spins_while_it_is_reopened()
    {
        var tree = new BrowseTree { Session = TestSession.Shared };
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath);
        var reopens = new Reopens();
        Trees.ShowReopening(tree, reopens.Files);
        var idle = tree.Row(assembly.Root).Spinning;

        reopens.Start(assembly.FilePath);

        Assert.False(idle);
        Assert.True(tree.Row(assembly.Root).Spinning);
        Assert.False(tree.Row(assembly.Root.Child(AppStateDriver.FixturesNamespace)).Spinning);
    }

    [Fact]
    public void Revealing_a_hidden_non_public_node_selects_its_nearest_shown_ancestor()
    {
        var d = new AppStateDriver();
        var tree = new BrowseTree { Session = TestSession.Shared };
        var root = new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath).Root;
        var ns = root.Child(AppStateDriver.FixturesNamespace);
        var secret = ns.Child("Hidden", "Secret() : int");
        Trees.ShowApi(tree, ApiVisibility.PublicOnly);

        Trees.RevealNode(tree, secret);
        Assert.Same(ns, tree.View.Selected);

        Trees.ShowApi(tree, ApiVisibility.AllButCompilerGenerated);
        Trees.RevealNode(tree, secret);
        Assert.Same(secret, tree.View.Selected);
    }

    [Fact]
    public void Compiler_generated_members_show_only_when_everything_is_shown()
    {
        var tree = new BrowseTree { Session = TestSession.Shared };
        var root = new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath).Root;
        var counter = root.Child(AppStateDriver.FixturesNamespace, "Counter");
        var backingField = counter.Child("<Count>k__BackingField : int");

        Assert.True(backingField.IsCompilerGenerated);
        Assert.DoesNotContain(backingField, tree.GetChildren(counter).Nodes);

        Trees.ShowApi(tree, ApiVisibility.All);

        Assert.Contains(backingField, tree.GetChildren(counter).Nodes);
    }

    [Fact]
    public void The_public_children_cache_does_not_keep_closed_assemblies_alive()
    {
        var tree = new BrowseTree { Session = TestSession.Shared };
        Trees.ShowApi(tree, ApiVisibility.PublicOnly);
        var assembly = FilterAndClose(tree);

        for (var i = 0; i < 100 && assembly.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(20);
        }

        Assert.False(assembly.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference FilterAndClose(BrowseTree tree)
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var assembly = list.Open(AppStateDriver.FixturePath);
        var root = assembly.Root;
        _ = root.Children;
        Assert.NotEmpty(tree.GetChildren(root).Nodes);
        AssemblyListing.CloseAssembly(list, assembly);
        return new WeakReference(assembly);
    }
}
