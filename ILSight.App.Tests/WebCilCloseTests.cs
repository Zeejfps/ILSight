using ZGF.Gui.Immediate;
using ILSight.CommandArea;
using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.App.Tests;

public sealed class WebCilCloseTests
{
    private static readonly string LargeAppPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "WebCil", "LargeApp.wasm");

    private static async Task<(AppDriver App, LoadedModule Module, string Path)> ShowingLargeAppAsync(TempDir folder)
    {
        // A copy, so the test may replace its file.
        var path = folder.Write("LargeApp.wasm", await File.ReadAllBytesAsync(LargeAppPath));
        var app = new AppDriver(height: 500f);
        app.Driver.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(path)], ct));
        app.Driver.RunUntil(() => app.AppState.Assemblies.Assemblies.Count == 1);
        app.Settle(1);
        var module = app.AppState.Assemblies.Assemblies.Single();
        app.Show(await module.ReadRootAsync().GetChildAsync("LargeApp", "Part159"), 2);
        return (app, module, path);
    }

    [Fact]
    public async Task A_webcil_assembly_closed_while_a_member_decompiles_keeps_the_tab_and_frees_its_file_once_the_reads_end()
    {
        using var folder = new TempDir();
        var (app, module, path) = await ShowingLargeAppAsync(folder);
        var whole = await File.ReadAllBytesAsync(path);
        var tab = app.AppState.Navigation.ActiveTab ?? throw new InvalidOperationException("Part159 was shown in no tab.");
        var code = tab.Document;

        var part158 = await module.ReadRootAsync().GetChildAsync("LargeApp", "Part158");
        app.Driver.StartShowing(part158, inNewTab: true);
        var decompiling = app.AppState.Navigation.ActiveTab ?? throw new InvalidOperationException("Part158 was shown in no tab.");
        app.Driver.InTurn(() => Commands.CloseAssemblies(app.AppState, [module]));
        app.Settle(2);
        var released = LoadedAssemblies.WaitForReleaseAsync(module, CancellationToken.None);
        app.Frames.WaitUntil(() => released.IsCompleted, AppStateDriver.Patience);
        await File.WriteAllBytesAsync(path, whole);

        Assert.Same(code, tab.Document);
        Assert.Equal("LargeApp.wasm", tab.Load.ClosedAssembly);
        Assert.Equal("LargeApp.wasm", decompiling.Load.ClosedAssembly);
        Assert.False(decompiling.Load.IsBusy);
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public async Task Switching_the_language_after_a_webcil_assembly_closed_starts_nothing_for_its_tab()
    {
        using var folder = new TempDir();
        var (app, module, _) = await ShowingLargeAppAsync(folder);
        var tab = app.AppState.Navigation.ActiveTab ?? throw new InvalidOperationException("Part159 was shown in no tab.");
        var code = tab.Document;
        app.Driver.InTurn(() => Commands.CloseAssemblies(app.AppState, [module]));
        app.Settle(2);

        app.AppState.Decompiler.UseLanguage(DecompilationLanguage.IL);
        app.Settle(2);

        Assert.Equal(DecompilationLanguage.CSharp, tab.Load.Language);
        Assert.Same(code, tab.Document);
        Assert.False(tab.Load.IsBusy);
        Assert.Empty(app.AppState.Errors.Entries);
    }

    private static Selector Crumb(AppDriver app, string label)
    {
        var crumb = app.Frames.Snapshot().Nodes.Single(n => IsCrumb(n) && n.SelfAndDescendants().Any(c => c.Label == label));
        Assert.NotNull(crumb.Path);
        return new Selector.ByPath(crumb.Path);
    }

    private static bool IsCrumb(SemanticNode node)
    {
        var path = node.Path;
        if (path is null)
        {
            return false;
        }

        return path.Segments[^1] is TestSegment.Named { Name: "crumb" };
    }

    [Fact]
    public async Task A_closed_webcil_assemblys_grid_says_so_where_rows_were_not_read_and_asks_for_nothing_scrolled_or_clicked()
    {
        using var folder = new TempDir();
        var path = folder.Write("LargeApp.wasm", await File.ReadAllBytesAsync(LargeAppPath));
        var app = new AppDriver(height: 500f);
        app.Driver.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(path)], ct));
        app.Driver.RunUntil(() => app.AppState.Assemblies.Assemblies.Count == 1);
        app.Settle(1);
        var module = app.AppState.Assemblies.Assemblies.Single();
        var strings = await module.ReadRootAsync().GetChildAsync("Metadata", "String Heap (357)");
        app.Show(strings, 2);
        var tab = app.AppState.Navigation.ActiveTab ?? throw new InvalidOperationException("The string heap was shown in no tab.");
        var grid = Assert.IsType<MetadataGrid.Paged>(tab.Grid);
        var state = DocumentPanels.GetView(app.Ui.Documents, tab).GetGridState(grid);
        var last = grid.RowCount - 1;
        var closed = app.Ui.Strings.DocumentClosed("LargeApp.wasm");
        app.Driver.InTurn(() => Commands.CloseAssemblies(app.AppState, [module]));
        app.Settle(2);

        state.Scroll.Y = 100000f;
        app.Run(2);
        var scrolled = (Loading: app.Frames.Drew(app.Ui.Strings.MetadataLoading), Asked: app.Ui.MetadataGridPages.HasAsked, Read: grid.IsRowRead(last));
        var rowsClosed = app.Frames.Drew(app.Ui.Strings.MetadataClosed);
        app.Frames.ClickOn(Crumb(app, "Metadata")).Frame(2);
        var clicked = (Node: tab.Node, Busy: tab.Load.IsBusy, Background: app.AppState.Work.Outstanding.Count);
        app.Settle(2);

        Assert.Equal((false, false, false), scrolled);
        Assert.True(rowsClosed);
        Assert.True(app.Frames.Drew(closed));
        Assert.Equal((strings, false, 0), clicked);
        Assert.Same(grid, tab.Grid);
        Assert.Equal("LargeApp.wasm", tab.Load.ClosedAssembly);
        Assert.Empty(app.AppState.Errors.Entries);
    }
}
