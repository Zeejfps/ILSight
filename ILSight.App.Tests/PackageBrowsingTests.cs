using System.IO.Compression;
using ILSight.Core;
using ILSight.Localization;
using static ILSight.Assemblies;
using static ILSight.Navigation;
using static ILSight.App.Tests.BuiltInGlyphs;

namespace ILSight.App.Tests;

public sealed class PackageBrowsingTests
{
    private const string PackageName = "Fixtures.1.0.0.nupkg";
    private const string LibFolder = "lib/net10.0";

    [Fact]
    public void An_opened_package_is_listed_in_the_tree_and_its_tab_lists_what_it_holds()
    {
        using var temp = new TempDir();
        var app = new AppDriver();

        OpenAssembly(app.AppState, WritePackage(temp));
        app.Frames.WaitUntil(() => app.AppState.Tabs.Active?.Node?.Icon == NodeIcon.Package, Patience);
        app.Settle(2);

        Assert.Equal(NodeIcon.Package, Assert.Single(app.Ui.Tree.Roots).Icon);
        Assert.True(app.Frames.Drew(PackageName));
        Assert.Contains($"{LibFolder}/", Documents.GetText(app.AppState.Tabs.Active!.Document));
    }

    [Fact]
    public void Going_to_a_type_in_a_package_reveals_it_in_the_tree_under_the_package()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);
        var greeter = Greeter(app);

        Navigate(app.AppState, greeter.Definition, inNewTab: false);
        app.Settle(2);

        Assert.Same(greeter, app.AppState.Tabs.Active!.Node);
        Assert.Same(greeter, app.Ui.Tree.View.Selected);
    }

    [Fact]
    public void Closing_from_a_node_inside_a_package_closes_the_package()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);

        CloseAssembly(app.AppState, Greeter(app).Assembly);
        app.Run(2);

        Assert.Empty(app.AppState.Assemblies.Listed);
        Assert.Empty(app.Ui.Tree.Roots);
    }

    [Fact]
    public void The_close_button_ticks_a_selected_package_but_not_for_an_assembly_inside_it()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);
        var package = app.AppState.Assemblies.Listed[0];

        app.Ui.Tree.View.Select(Greeter(app).Assembly.Root);
        app.Run(2);
        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/close-assembly")).Frame(2);
        Assert.False(Assert.Single(app.Ui.Closer.Order).Included);
        AssemblyClosing.CloseCloser(app.Ui.Closer);

        app.Ui.Tree.View.Select(package.Root);
        app.Run(2);
        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/close-assembly")).Frame(2);
        Assert.True(Assert.Single(app.Ui.Closer.Order).Included);
        app.Frames.ClickOn(Selector.AtPath("close-ticked")).Frame(2);

        Assert.Empty(app.AppState.Assemblies.Listed);
    }

    [Fact]
    public void An_assembly_in_a_package_is_named_in_results_with_the_folder_it_sits_in()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);
        var assembly = Greeter(app).Assembly;

        var label = NodeText.AssemblyLabel(assembly);

        Assert.Equal($"{assembly.NameAndVersion} · {LibFolder}", label);
    }

    [Fact]
    public void Exporting_offers_the_assemblies_of_an_open_package_with_the_picked_one_ticked()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);
        var assembly = Assert.IsType<LoadedModule>(Greeter(app).Assembly);

        app.OpenExportSetup(assembly);

        var choice = Assert.Single(app.Ui.ExportSetup.Choices);
        Assert.Same(assembly, choice.Assembly);
        Assert.True(choice.Included);
    }

    [Fact]
    public void The_tree_menu_extracts_the_file_of_an_assembly_in_a_package()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);
        var assembly = Greeter(app).Assembly;
        var path = temp.PathOf("Extracted.dll");
        Trees.RevealNode(app.Ui.Tree, assembly.Root);
        app.Run(2);

        app.Frames.FilePicker.Answer = path;
        app.PickFromTreeMenu(assembly.Name, "Extract file…");

        Assert.Equal(Path.GetFileName(AppStateDriver.FixturePath), app.Frames.FilePicker.LastSave.FileName);
        Assert.Equal(File.ReadAllBytes(AppStateDriver.FixturePath), File.ReadAllBytes(path));
    }

    [Fact]
    public void The_tree_menu_extracts_every_file_of_a_package_in_the_background()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);
        var folder = temp.PathOf("out");

        app.Frames.FilePicker.Answer = folder;
        app.PickFromTreeMenu(PackageName, "Extract all files…");
        app.Frames.WaitUntil(() => app.AppState.FinishedExports.Count == 1, Patience);

        var report = app.AppState.FinishedExports[0];
        var dll = Path.Combine(folder, "lib", "net10.0", Path.GetFileName(AppStateDriver.FixturePath));
        Assert.Equal(ExportKind.Files, report.Job.Kind);
        var finished = Assert.IsType<ExportEnd.Finished>(report.End);
        Assert.Empty(finished.Problems);
        Assert.Equal([dll, Path.Combine(folder, "readme.txt")], finished.Written);
        Assert.Equal("Read me", File.ReadAllText(Path.Combine(folder, "readme.txt")));
    }

    private static AppDriver OpenedPackage(TempDir temp)
    {
        var app = new AppDriver();
        OpenAssembly(app.AppState, WritePackage(temp));
        app.Frames.WaitUntil(() => app.AppState.Tabs.Active?.Node?.Icon == NodeIcon.Package, Patience);
        app.Settle(2);
        return app;
    }

    private static BrowseNode Greeter(AppDriver app)
    {
        var package = app.AppState.Assemblies.Listed[0];
        var fixtures = package.Root.Child(LibFolder).Children.Single(IsAssembly);
        return fixtures.Child(AppStateDriver.FixturesNamespace, "Greeter");
    }

    private static bool IsAssembly(BrowseNode node)
    {
        return node.Icon == NodeIcon.Assembly;
    }

    /// <summary>A NuGet package of the fixture assembly, beside a text file.</summary>
    private static string WritePackage(TempDir temp)
    {
        var path = temp.Write(PackageName, []);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        var name = Path.GetFileName(AppStateDriver.FixturePath);
        archive.CreateEntryFromFile(AppStateDriver.FixturePath, $"{LibFolder}/{name}");
        var readme = archive.CreateEntry("readme.txt");
        using var writer = new StreamWriter(readme.Open());
        writer.Write("Read me");
        return path;
    }
}
