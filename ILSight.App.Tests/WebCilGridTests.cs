using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.MetadataGridArea;

namespace ILSight.App.Tests;

public sealed class WebCilGridTests
{
    private static readonly string WebCilSamplePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "WebCil", "SampleApp.wasm");

    /// <summary>A WebCIL fixture over 64 KB, whose string heap (357 rows) and MethodDef table (800 rows) are longer than
    /// a grid's first read.</summary>
    private static readonly string WebCilLargePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "WebCil", "LargeApp.wasm");

    /// <summary>An app that has opened the WebCIL sample, settled.</summary>
    private static AppDriver OpenWebCil()
    {
        return OpenWebCil(new AppDriver(height: 500f), WebCilSamplePath);
    }

    /// <summary><paramref name="app"/> once it has opened the WebCIL file at <paramref name="path"/>, settled.</summary>
    private static AppDriver OpenWebCil(AppDriver app, string path)
    {
        app.Driver.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(path)], ct));
        app.Driver.RunUntil(() => app.AppState.Assemblies.Assemblies.Count == 1);
        app.Settle(1);
        return app;
    }

    private static async Task<BrowseNode> MetadataAsync(AppDriver app, params string[] labels)
    {
        return await app.AppState.Assemblies.Assemblies.Single().ReadRootAsync().GetChildAsync("Metadata").GetChildAsync(labels);
    }

    [Fact]
    public async Task A_webcil_modules_grid_opens_showing_its_rows_with_no_frame_of_loading_after_its_contents()
    {
        var app = OpenWebCil();
        var typeDefs = await (await MetadataAsync(app, "Tables")).TableAsync("TypeDef");
        var loading = app.Ui.Strings.MetadataLoading;
        // What each frame drew, read before the next one pumps the app state: whether the tab showed its grid then.
        var frames = new List<(bool Grid, bool Rows, bool Loading)>();
        var pump = app.Frames.BeforeFrame;
        app.Frames.BeforeFrame = () =>
        {
            frames.Add((app.AppState.Navigation.ActiveTab?.Grid is not null, app.Frames.Drew("Counter"), app.Frames.Drew(loading)));
            pump?.Invoke();
        };

        app.Driver.StartShowing(typeDefs);
        app.Settle(2);
        frames.Add((app.AppState.Navigation.ActiveTab?.Grid is not null, app.Frames.Drew("Counter"), app.Frames.Drew(loading)));

        var shown = frames.Where(static f => f.Grid).ToList();
        Assert.IsType<MetadataGrid.Paged>(app.AppState.Navigation.ActiveTab?.Grid);
        Assert.NotEmpty(shown);
        Assert.All(shown, static f => Assert.Equal((true, false), (f.Rows, f.Loading)));
    }

    [Fact]
    public async Task Scrolling_a_webcil_grid_larger_than_a_window_shows_loading_for_the_page_then_its_rows()
    {
        var app = OpenWebCil(new AppDriver(height: 500f), WebCilLargePath);
        app.Show(await MetadataAsync(app, "String Heap (357)"), 2);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var grid = Assert.IsType<MetadataGrid.Paged>(tab.Grid);
        var state = DocumentPanels.GetGridState(DocumentPanels.GetView(app.Ui.Documents, tab), grid);
        var loading = app.Ui.Strings.MetadataLoading;
        var last = grid.RowCount - 1;
        var before = app.Frames.Drew(loading);

        state.Scroll.Y = 100000f;
        app.Run(1);
        var scrolled = (app.Frames.Drew(loading), grid.IsRowRead(last));
        app.Settle(1);

        Assert.False(before);
        Assert.Equal((true, false), scrolled);
        Assert.True(grid.IsRowRead(last));
        Assert.False(app.Frames.Drew(loading));
        app.TextRect(MetadataGrids.GetText(grid, last, grid.Column("Offset")));
    }

    [Fact]
    public async Task A_page_that_could_not_be_read_shows_why_in_each_of_its_cells()
    {
        var app = OpenWebCil(new AppDriver(height: 500f), WebCilLargePath);
        app.Show(await MetadataAsync(app, "String Heap (357)"), 1);
        var grid = Assert.IsType<MetadataGrid.Paged>(app.AppState.Navigation.ActiveTab?.Grid);
        var row = MetadataGrid.Paged.RowsPerPage * MetadataGrid.Paged.CountPages(MetadataGridFrame.SampleRows);
        Assert.False(grid.IsRowRead(row));
        var handler = new ReadMetadataPageHandler(new FailingReadMetadataPageHandlerPorts());

        await app.Driver.RunAsync(() => handler.HandleAsync(new MetadataGridPageAsk { Grid = grid, Page = MetadataGrid.Paged.PageOf(row) }, CancellationToken.None));

        Assert.True(grid.IsRowRead(row));
        Assert.Equal("?", MetadataGrids.GetText(grid, row, grid.Column("Value")));
        Assert.Equal(FailingReadMetadataPageHandlerPorts.Why, MetadataGrids.FindTooltip(grid, row, grid.Column("Value"))?.Text);
    }

    [Fact]
    public async Task A_webcil_grid_measures_its_columns_over_the_same_rows_as_a_grid_in_memory()
    {
        var app = OpenWebCil(new AppDriver(height: 500f, measurer: new WideDigits()), WebCilLargePath);

        app.Show(await (await MetadataAsync(app, "Tables")).TableAsync("MethodDef"), 2);

        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var grid = Assert.IsType<MetadataGrid.Paged>(tab.Grid);
        var columns = DocumentPanels.GetGridState(DocumentPanels.GetView(app.Ui.Documents, tab), grid).Columns;
        Assert.NotNull(columns);
        var widest = MetadataGrids.GetText(grid, MetadataGridFrame.SampleRows - 1, grid.Column("RID"));
        Assert.True(grid.IsRowRead(MetadataGridFrame.SampleRows - 1));
        Assert.True(columns.Width(grid.Column("RID")) >= WideDigits.Measure(widest),
            $"The RID column is {columns.Width(grid.Column("RID"))} wide; row {MetadataGridFrame.SampleRows}'s RID, {widest}, needs {WideDigits.Measure(widest)}.");
    }

    [Fact]
    public async Task A_webcil_grid_opened_in_a_tab_taller_than_its_measured_rows_shows_no_loading_after_its_content()
    {
        var app = OpenWebCil(new AppDriver(height: 8000f), WebCilLargePath);
        app.Run(2);
        var methods = await (await MetadataAsync(app, "Tables")).TableAsync("MethodDef");
        var loading = app.Ui.Strings.MetadataLoading;
        // What each frame drew, read before the next one pumps the app state: whether the tab showed its grid then.
        var frames = new List<(bool Grid, bool Loading)>();
        var pump = app.Frames.BeforeFrame;
        app.Frames.BeforeFrame = () =>
        {
            frames.Add((app.AppState.Navigation.ActiveTab?.Grid is not null, app.Frames.Drew(loading)));
            pump?.Invoke();
        };

        app.Driver.StartShowing(methods);
        app.Settle(2);
        frames.Add((app.AppState.Navigation.ActiveTab?.Grid is not null, app.Frames.Drew(loading)));

        var grid = Assert.IsType<MetadataGrid.Paged>(app.AppState.Navigation.ActiveTab?.Grid);
        var shown = frames.Where(static f => f.Grid).ToList();
        Assert.True(app.AppState.FirstGridRows > MetadataGridFrame.SampleRows, $"The tab area shows {app.AppState.FirstGridRows} rows.");
        Assert.True(grid.IsRowRead(app.AppState.FirstGridRows - 1));
        Assert.NotEmpty(shown);
        Assert.All(shown, static f => Assert.False(f.Loading));
    }
}
