using System.IO.Compression;
using ILSight.Core;
using ILSight.ExportArea;
using ILSight.AssemblyArea;
using ILSight.CommandArea;

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

        var packagePath = WritePackage(temp);
        app.Open([AssemblyPath.Of(packagePath)]);
        app.Frames.WaitUntil(() => app.AppState.ListSession.Navigation.ActiveTab?.Node?.Icon == NodeIcon.Package, Waits.Patience);
        app.Settle(2);

        Assert.Equal(NodeIcon.Package, Assert.Single(app.Ui.Tree.Roots).Icon);
        Assert.True(app.Frames.Drew(PackageName));
        var tab = app.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.Contains($"{LibFolder}/", Documents.GetText(tab.Document));
    }

    [Fact]
    public async Task Going_to_a_type_in_a_package_reveals_it_in_the_tree_under_the_package()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);
        var greeter = await GreeterAsync(app);

        app.Start((a, ct) =>
        {
            var resolveTicket = a.ListSession.Navigation.BeginResolving(inNewTab: false);
            return a.NavigateHandler.HandleAsync(a.ListSession.Navigation, a.Decompiler, greeter.Definition, resolveTicket, ct);
        });
        app.Settle(2);

        Assert.Same(greeter, app.AppState.ListSession.Navigation.ActiveTab?.Node);
        Assert.Same(greeter, app.Ui.Tree.View.Selected);
    }

    [Fact]
    public async Task Closing_from_a_node_inside_a_package_closes_the_package()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);

        var greeter = await GreeterAsync(app);
        var assembly = greeter.Assembly;
        app.Driver.InTurn(() => Commands.CloseAssemblies(app.AppState, [assembly]));
        app.Run(2);

        Assert.Empty(app.AppState.ListSession.Assemblies.Listed);
        Assert.Empty(app.Ui.Tree.Roots);
    }

    [Fact]
    public async Task The_close_button_ticks_a_selected_package_but_not_for_an_assembly_inside_it()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);
        var package = app.AppState.ListSession.Assemblies.Listed[0];

        var greeter = await GreeterAsync(app);
        var root = await greeter.Assembly.ReadRootAsync();
        app.Ui.Tree.View.Select(root);
        app.Run(2);
        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/close-assembly"));
        app.Frames.Frame(2);
        Assert.False(Assert.Single(app.Ui.Closer.Rows.Order).Included);
        app.Ui.Closer.Close();

        var packageRoot = await package.ReadRootAsync();
        app.Ui.Tree.View.Select(packageRoot);
        app.Run(2);
        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/close-assembly"));
        app.Frames.Frame(2);
        Assert.True(Assert.Single(app.Ui.Closer.Rows.Order).Included);
        app.Frames.ClickOn(Selector.AtPath("close-ticked"));
        app.Frames.Frame(2);

        Assert.Empty(app.AppState.ListSession.Assemblies.Listed);
    }

    [Fact]
    public async Task An_assembly_in_a_package_is_named_in_results_with_the_folder_it_sits_in()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);
        var greeter = await GreeterAsync(app);
        var assembly = greeter.Assembly;

        var label = NodeText.AssemblyLabel(assembly);

        Assert.Equal($"{assembly.NameAndVersion} · {LibFolder}", label);
    }

    [Fact]
    public async Task Exporting_offers_the_assemblies_of_an_open_package_with_the_picked_one_ticked()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);
        var greeter = await GreeterAsync(app);
        var assembly = Assert.IsType<LoadedModule>(greeter.Assembly);

        app.OpenExportSetup(assembly);

        var choice = Assert.Single(app.Ui.ExportSetup.Assemblies.Choices);
        Assert.Same(assembly, choice.Assembly);
        Assert.True(choice.Included);
    }

    [Fact]
    public async Task The_tree_menu_extracts_the_file_of_an_assembly_in_a_package()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);
        var greeter = await GreeterAsync(app);
        var assembly = greeter.Assembly;
        var path = temp.PathOf("Extracted.dll");
        var root = await assembly.ReadRootAsync();
        app.Ui.Tree.RevealNode(root);
        app.Run(2);

        app.Frames.FilePicker.Answer = path;
        app.PickFromTreeMenu(assembly.Name, "Extract file…");
        app.Settle();

        Assert.Equal(Path.GetFileName(AppStateDriver.FixturePath), app.Frames.FilePicker.LastSave.FileName);
        Assert.Equal(await File.ReadAllBytesAsync(AppStateDriver.FixturePath), await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public void The_tree_menu_extracts_every_file_of_a_package_in_the_background()
    {
        using var temp = new TempDir();
        var app = OpenedPackage(temp);
        var folder = temp.PathOf("out");

        app.Frames.FilePicker.Answer = folder;
        app.PickFromTreeMenu(PackageName, "Extract all files…");
        app.Frames.WaitUntil(() => app.AppState.FinishedExports.Count == 1, Waits.Patience);

        var report = app.AppState.FinishedExports[0];
        var dll = Path.Combine(folder, "lib", "net10.0", Path.GetFileName(AppStateDriver.FixturePath));
        Assert.Equal(ExportKind.Files, report.Job.Kind);
        var finished = UnionAsserts.GetCase<ExportEnd.Finished>(report.End);
        Assert.Empty(finished.Problems);
        Assert.Equal([dll, Path.Combine(folder, "readme.txt")], finished.Written);
        Assert.Equal("Read me", File.ReadAllText(Path.Combine(folder, "readme.txt")));
    }

    private static AppDriver OpenedPackage(TempDir temp)
    {
        var app = new AppDriver();
        var packagePath = WritePackage(temp);
        app.Open([AssemblyPath.Of(packagePath)]);
        app.Frames.WaitUntil(() => app.AppState.ListSession.Navigation.ActiveTab?.Node?.Icon == NodeIcon.Package, Waits.Patience);
        app.Settle(2);
        return app;
    }

    private static async Task<BrowseNode> GreeterAsync(AppDriver app)
    {
        var package = app.AppState.ListSession.Assemblies.Listed[0];
        var packageRoot = await package.ReadRootAsync();
        var lib = await packageRoot.GetChildAsync(LibFolder);
        var libChildren = await lib.ListChildrenAsync();
        var fixtures = libChildren.Single(IsAssembly);
        return await fixtures.GetChildAsync(AppStateDriver.FixturesNamespace, "Greeter");
    }

    private static bool IsAssembly(BrowseNode node)
    {
        return node.Icon == NodeIcon.Assembly;
    }

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
