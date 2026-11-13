using System.IO.Compression;
using ZGF.Gui;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.AssemblyListArea;
using static ILSight.AssemblyListArea.AssemblyLists;
using static ILSight.SettingArea.Settings;
using static ILSight.App.Tests.TestPaths;
using ILSight.SettingArea;

namespace ILSight.App.Tests;

public sealed class TargetFrameworkTests
{
    private static readonly TargetFrameworkId Net48 = Frameworks.Net48;
    private static readonly string SampleAppPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleApp", "SampleApp.dll");

    private static LoadedModule FixturesOf(AppState appState)
    {
        return appState.Assemblies.Assemblies.Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
    }

    /// <summary>Right-clicks the fixture assembly's row and picks <paramref name="item"/> from its
    /// Target framework submenu, then waits for the reload.</summary>
    private static void PickFramework(AppDriver app, string item)
    {
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.TreeItem, FixturesOf(app.AppState).Name), PointerButton.Right).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Target framework")).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, item)).Frame();
        app.Settle(2);
    }

    [Fact]
    public async Task A_framework_picked_from_the_tree_menu_resolves_the_assembly_after_the_reload_and_shows_in_its_row()
    {
        using var temp = new TempDir();
        var app = new AppDriver(width: 1600f, settings: temp.Settings());
        app.Driver.OpenFixtures();
        app.Settle(2);

        PickFramework(app, "net48");
        Assert.IsType<SettingsWriteResult.Written>(app.AppState.SettingsSaving.Save(app.AppState));

        var assembly = FixturesOf(app.AppState);
        Assert.True(assembly.IsTargetFrameworkPicked);
        Assert.Equal(Net48, assembly.TargetFramework);
        Assert.Equal("net48", NodeText.PickedFramework(await assembly.ReadRootAsync()));
        Assert.DoesNotContain("net48", NodeText.Secondary(await assembly.ReadRootAsync()));
        Assert.Equal(FontWeight.Bold, app.Frames.Canvas.Texts.Single(t => t.Inputs.Text == "net48").Inputs.Style.FontWeight.Value);
        Assert.Equal(Net48.Text, Assert.Single(temp.Settings().AssemblyLists).Frameworks?[PathOf(AppStateDriver.FixturePath)]);
    }

    [Fact]
    public async Task Picking_detected_goes_back_to_the_framework_the_assembly_was_built_for()
    {
        var app = AppDriver.Opened(width: 1600f);
        app.Settle(2);
        var detected = FixturesOf(app.AppState).DetectedTargetFramework?.Moniker;
        Assert.NotNull(detected);
        PickFramework(app, "net48");

        PickFramework(app, $"Detected ({detected})");

        var assembly = FixturesOf(app.AppState);
        Assert.False(assembly.IsTargetFrameworkPicked);
        Assert.Equal(assembly.DetectedTargetFramework, assembly.TargetFramework);
        Assert.Null(NodeText.PickedFramework(await assembly.ReadRootAsync()));
        Assert.Contains(detected, NodeText.Secondary(await assembly.ReadRootAsync()));
        Assert.Empty(app.AppState.Assemblies.TargetFrameworks.All);
    }

    [Fact]
    public async Task Picking_a_framework_reopens_only_that_assembly_with_the_tree_as_it_was_on_every_frame()
    {
        var app = AppDriver.Opened(width: 1600f);
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(SampleAppPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.IsListed(PathOf(SampleAppPath)), Patience);
        var fixtures = FixturesOf(app.AppState);
        var sample = SampleOf(app.AppState);
        var tree = app.Ui.Tree;
        foreach (var node in new[] { await fixtures.ReadRootAsync(), await fixtures.ReadRootAsync().GetChildAsync(AppStateDriver.FixturesNamespace), await sample.ReadRootAsync() })
        {
            tree.View.SetExpanded(node, true);
        }

        tree.View.Refresh();
        var greeter = await app.FixtureAsync("Greeter");
        app.Show(greeter, 2);
        var before = tree.ListExpandedPaths();
        var shapes = new List<List<NodePath>>();

        app.Start((a, ct) => a.AssemblyReloading.SetTargetFrameworkAsync(a.ListSession, a.Reopens, a.Decompiler, fixtures, Net48, ct));
        Poll.Until(() => FixturesOf(app.AppState) != fixtures, RunFrame);
        RunFrame();

        Assert.All(shapes, shape => Assert.Equal(before, shape));
        Assert.Same(sample, SampleOf(app.AppState));
        var selected = tree.View.Selected;
        Assert.NotNull(selected);
        Assert.Equal(greeter.Path, selected.Path);
        Assert.NotSame(greeter, tree.View.Selected);
        Assert.Same(FixturesOf(app.AppState), app.AppState.Navigation.ActiveTab?.Node?.Assembly);
        Assert.Empty(app.AppState.Errors.Entries);

        void RunFrame()
        {
            app.Run();
            shapes.Add(tree.ListExpandedPaths());
        }
    }

    private static LoadedModule SampleOf(AppState appState)
    {
        return appState.Assemblies.Assemblies.Single(a => a.FilePath.Text == SampleAppPath);
    }

    [Fact]
    public async Task Picking_a_framework_for_an_assembly_in_a_package_keeps_the_tree_expanded_and_the_tab_on_what_it_showed()
    {
        using var temp = new TempDir();
        var app = new AppDriver();
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(WritePackage(temp))], ct));
        app.Frames.WaitUntil(() => app.AppState.Navigation.ActiveTab?.Node?.Icon == NodeIcon.Package, Patience);
        app.Settle(2);
        var package = await app.AppState.Assemblies.Listed[0].ReadRootAsync();
        var folder = await package.GetChildAsync("lib/net10.0");
        var assembly = (await folder.ListChildrenAsync()).Single(c => c.Icon == NodeIcon.Assembly);
        var tree = app.Ui.Tree;
        foreach (var node in new[] { package, folder, assembly, await assembly.GetChildAsync(AppStateDriver.FixturesNamespace) })
        {
            tree.View.SetExpanded(node, true);
        }

        tree.View.Refresh();
        app.Show(await assembly.GetChildAsync(AppStateDriver.FixturesNamespace, "Greeter"), 2);
        var before = tree.ListExpandedPaths();

        app.Start((a, ct) => a.AssemblyReloading.SetTargetFrameworkAsync(a.ListSession, a.Reopens, a.Decompiler, Assert.IsType<LoadedModule>(assembly.Assembly), Net48, ct));
        app.Settle(3);

        Assert.Equal(before, tree.ListExpandedPaths());
        Assert.Equal("Greeter", app.AppState.Navigation.ActiveTab?.Node?.Text);
        Assert.Empty(app.AppState.Errors.Entries);
    }

    /// <summary>A NuGet package holding the fixture assembly.</summary>
    private static string WritePackage(TempDir temp)
    {
        var path = temp.Write("Fixtures.1.0.0.nupkg", []);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        archive.CreateEntryFromFile(AppStateDriver.FixturePath, "lib/net10.0/" + Path.GetFileName(AppStateDriver.FixturePath));
        return path;
    }

    [Fact]
    public void A_picked_framework_is_kept_with_the_list_for_the_next_run()
    {
        using var temp = new TempDir();
        var settings = temp.Settings();
        settings.AssemblyLists =
        [
            new AssemblyListRecord
            {
                Name = SavedAssemblyLists.DefaultName,
                Assemblies = [PathOf(AppStateDriver.FixturePath)],
                Frameworks = new Dictionary<AssemblyPath, string> { [PathOf(AppStateDriver.FixturePath)] = Net48.Text },
            },
        ];
        var d = new AppStateDriver(settings);

        d.Settle();

        Assert.Equal(Net48, FixturesOf(d.AppState).TargetFramework);
    }

    [Fact]
    public void A_saved_framework_that_is_not_a_framework_id_is_dropped_and_the_assembly_takes_the_one_it_was_built_for()
    {
        using var temp = new TempDir();
        var settings = temp.Settings();
        settings.AssemblyLists =
        [
            new AssemblyListRecord
            {
                Name = SavedAssemblyLists.DefaultName,
                Assemblies = [PathOf(AppStateDriver.FixturePath)],
                Frameworks = new Dictionary<AssemblyPath, string> { [PathOf(AppStateDriver.FixturePath)] = @"C:\symbols\Fixtures.pdb" },
            },
        ];
        var d = new AppStateDriver(settings);

        d.Settle();

        var assembly = FixturesOf(d.AppState);
        Assert.False(assembly.IsTargetFrameworkPicked);
        Assert.Equal(assembly.DetectedTargetFramework, assembly.TargetFramework);
        Assert.Empty(d.AppState.Assemblies.TargetFrameworks.All);
    }
}
