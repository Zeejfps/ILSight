using ILSight.SearchArea;
using ILSight.ProcessArea;
using ILSight.FrameworkArea;
using ILSight.Core;
using ILSight.AssemblyArea;
using ILSight.CommandArea;
using ILSight.DiagramArea;
using ILSight.ExportArea;
using ILSight.BookmarkArea;

namespace ILSight.App.Tests;

/// <summary>Work whose start left a state pending that ends with an unexpected exception: reported once, and the state
/// settled, rather than pending for good.</summary>
public sealed class SettlingWorkflowsTests
{
    private static readonly string LeftPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiDiff", "Older", "ApiDiffSample.dll");
    private static readonly string RightPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiDiff", "Newer", "ApiDiffSample.dll");

    private static AppProblem.Unexpected ReportedOnce(AppState appState)
    {
        return Assert.IsType<AppProblem.Unexpected>(Assert.Single(appState.Errors.Entries).Problem);
    }

    [Fact]
    public void A_search_run_that_throws_is_reported_once_and_the_search_shows_it_failed_saying_why()
    {
        var ports = new FakeRunSearchHandlerPorts { Found = [], Throws = new InvalidOperationException("broken") };
        var app = new AppDriver(hooks: new AdapterHooks { Searching = _ => ports });
        app.Driver.OpenFixtures();
        ScreenLayout.OpenSearch(app.Ui);
        app.Run();

        app.Frames.Type("Greeter").Frame();
        app.RunOut();

        Assert.False(app.Ui.Search.Results.IsBusy);
        Assert.Equal("broken", app.Ui.Search.Results.RunFailure);
        Assert.Contains(app.Frames.TextRects(), static t => t.Text == "Search failed (broken)");
        Assert.Equal("broken", ReportedOnce(app.AppState).Error.Message);
    }

    [Fact]
    public async Task An_export_that_throws_is_reported_once_and_stays_in_the_exports_panel_failed()
    {
        var ports = new FakeExportWritingPorts { CodeFileThrows = new InvalidOperationException("broken") };
        var d = AppStateDriver.OpenedWith(new AdapterHooks { Exporting = _ => ports });
        var greeter = await d.FixtureAsync("Greeter");
        using var temp = new TempDir();

        d.InTurn(() => ExportCommands.SaveCode(d.AppState, greeter, temp.PathOf("Greeter.cs")));
        d.RunUntil(() => d.AppState.FinishedExports.Count == 1);
        d.Settle();

        var failed = Assert.IsType<ExportEnd.Failed>(Assert.Single(d.AppState.FinishedExports).End);
        Assert.Equal("broken", failed.Error.Message);
        Assert.False(d.AppState.Export.IsRunning);
        Assert.Equal("broken", ReportedOnce(d.AppState).Error.Message);
    }

    [Fact]
    public void A_comparison_run_that_throws_is_reported_once_and_the_comparison_shows_failed()
    {
        var ports = new FakeCompareAssembliesHandlerPorts();
        var d = AppStateDriver.OpenedWith(new AdapterHooks { Comparing = _ => ports });
        d.Open([TestPaths.PathOf(LeftPath), TestPaths.PathOf(RightPath)]);
        d.RunUntil(() => d.AppState.ListSession.Assemblies.Assemblies.Count == 3);
        d.Settle();
        var left = d.AppState.ListSession.Assemblies.Assemblies.Single(a => a.FilePath.Text == LeftPath);
        var right = d.AppState.ListSession.Assemblies.Assemblies.Single(a => a.FilePath.Text == RightPath);
        d.StartComparing(Comparisons.CompareAssemblies(d.AppState.ListSession.Navigation, d.AppState.ListSession.Assemblies, left, right, Names.Comparison(left)));
        d.RunUntil(() => ports.Answers.Count == 1);
        var comparison = d.AppState.ListSession.Navigation.ActiveTab?.Comparison;
        Assert.NotNull(comparison);
        Assert.True(comparison.Work.IsRunning);

        ports.Answers[0].SetException(new InvalidOperationException("broken"));
        d.Settle();

        Assert.False(comparison.Work.IsRunning);
        Assert.IsType<ComparisonFailure.Failed>(Assert.IsType<ComparisonResult.Failed>(comparison.Result).Failure);
        Assert.Equal("broken", ReportedOnce(d.AppState).Error.Message);
    }

    [Fact]
    public async Task A_diagram_read_that_throws_is_reported_once_and_the_diagram_shows_it_failed_saying_why()
    {
        var ports = new FakeDiagramGrowingPorts();
        var d = AppStateDriver.OpenedWith(new AdapterHooks { Growing = _ => ports });
        var greeter = (await d.FixtureAsync("Greeter")).Definition;
        Assert.NotNull(greeter);
        d.InTurn(() => DiagramView.ShowInNewDiagramLater(d.Ui, greeter, "Greeter"));
        d.RunUntil(() => ports.Requests.Count == 1);
        var diagram = d.AppState.ListSession.Navigation.ActiveTab?.Diagram;
        Assert.NotNull(diagram);
        Assert.True(diagram.IsBusy);

        ports.Requests[0].Answer.SetException(new InvalidOperationException("broken"));
        d.Settle();

        Assert.False(diagram.IsBusy);
        Assert.Equal("broken", diagram.ReadFailure);
        Assert.Equal("broken", ReportedOnce(d.AppState).Error.Message);
    }

    [Fact]
    public void An_opening_whose_workflow_fails_stops_opening_unless_a_newer_request_began()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var navigation = d.AppState.ListSession.Navigation;
        var path = AssemblyPath.Of(AppStateDriver.FixturePath);

        var opening = navigation.BeginOpening([path]);
        navigation.FailOpening();
        var stopped = navigation.IsOpening;
        var older = navigation.BeginOpening([path]);
        navigation.BeginRequest();
        navigation.FailOpening();

        Assert.NotNull(opening);
        Assert.NotNull(older);
        Assert.False(stopped);
        Assert.False(navigation.IsOpening);
    }

    [Fact]
    public void A_reload_whose_workflow_fails_ends_and_its_tabs_say_why_keeping_their_place()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var session = d.AppState.ListSession;
        var tab = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var location = tab.Location;
        Assert.NotNull(location);
        var ticket = session.Reload.Start();
        tab.Load.ShowReloading(location);

        var loads = AssemblyReloading.FailReload(session, ticket, new InvalidOperationException("broken"));

        Assert.False(session.Reload.IsRunning);
        var failed = Assert.IsType<TabContent.Unreadable>(tab.Content);
        Assert.Equal("broken", failed.Why);
        Assert.Equal(location, failed.Location);
        Commands.StartLoads(d.AppState, loads);
    }

    [Fact]
    public void A_reopen_whose_workflow_fails_no_longer_shows_its_file_reopening()
    {
        var reopens = new Reopens();
        var path = AssemblyPath.Of(AppStateDriver.FixturePath);
        var ticket = reopens.Start(path);

        reopens.Stop(path);

        Assert.False(reopens.IsRunning);
        Assert.False(reopens.TryFinish(path, ticket));
    }

    [Fact]
    public void A_list_reopening_whose_workflow_fails_ends_as_one_that_failed()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var session = d.AppState.ListSession;
        var reopening = session.BeginReopening(d.AppState.Lists);

        var loads = OpenAssembliesAsReloadHandler.FailOpeningAsReload(session, reopening.Reload);

        Assert.False(session.Reload.IsRunning);
        Commands.StartLoads(d.AppState, loads);
    }

    [Fact]
    public void A_runtime_or_process_scan_whose_workflow_fails_ends_failed_saying_why()
    {
        var d = AppStateDriver.Opened();
        var runtime = d.Ui.Picker.Browser;
        var processes = d.Ui.ProcessPicker.Browser;
        var runtimeScan = runtime.Open();
        Assert.NotNull(runtimeScan);
        var processScan = processes.StartScan();

        runtime.FailScan(runtimeScan, new InvalidOperationException("broken"));
        processes.FailScan(processScan, new InvalidOperationException("broken"));

        Assert.Equal("broken", Assert.IsType<RuntimeScan.Failed>(runtime.Scan).Error);
        Assert.Equal("broken", Assert.IsType<ProcessScan.Failed>(processes.Scan).Error);
    }

    [Fact]
    public void A_sort_whose_workflow_fails_ends_with_the_header_showing_the_order_the_rows_are_in()
    {
        var results = new SearchResults();
        var before = results.AskedOrder;
        var sorting = results.StartSort(SearchSort.Name);

        results.FailSort(sorting);

        Assert.False(results.IsBusy);
        Assert.Equal(before, results.AskedOrder);
    }

    [Fact]
    public void A_preset_look_whose_workflow_fails_is_asked_again_by_the_next_menu()
    {
        var scan = new PresetScan();
        scan.AskForPresets();
        Assert.True(scan.TryStart());

        scan.Fail();
        scan.AskForPresets();

        Assert.True(scan.TryStart());
        Assert.Null(scan.Found);
    }

    [Fact]
    public async Task A_bookmark_lookup_that_throws_is_reported_once_and_stops_resolving()
    {
        var ports = new FakeGoToBookmarkHandlerPorts();
        var d = AppStateDriver.OpenedWith(new AdapterHooks { GoingToBookmarks = _ => ports });
        d.Show(await d.FixtureAsync("Outline"));
        var tab = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.IsType<BookmarkToggle.Added>(Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("return First();").Line, Names.Bookmark));
        var bookmark = Assert.Single(d.AppState.Bookmarks.Items);

        d.InTurn(() => Commands.StartGoingToBookmark(d.AppState, bookmark));
        d.RunUntil(() => ports.Answers.Count == 1);
        var resolving = d.AppState.ListSession.Navigation.IsResolving;
        ports.Answers[0].SetException(new InvalidOperationException("broken"));
        d.Settle();

        Assert.True(resolving);
        Assert.False(d.AppState.ListSession.Navigation.IsResolving);
        Assert.Equal("broken", ReportedOnce(d.AppState).Error.Message);
    }
}
