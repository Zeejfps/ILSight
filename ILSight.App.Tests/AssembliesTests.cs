using ILSight.Core;
using ILSight.AssemblyArea;
using static ILSight.AssemblyArea.Assemblies;
using static ILSight.AssemblyArea.AssemblyReloading;

namespace ILSight.App.Tests;

public sealed class AssembliesTests
{
    private static LoadedModule FixturesOf(AppStateDriver d)
    {
        return d.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
    }

    private static AssemblyPath PathTo(string file)
    {
        return Assert.IsType<AssemblyPath>(AssemblyPath.Parse(Path.Combine(Path.GetTempPath(), file)));
    }

    /// <summary>The lookups of the nodes reopened tabs wait for among <paramref name="loads"/>.</summary>
    private static List<ReopenedNodesLookup> ListLookups(TabLoads loads)
    {
        var lookups = new List<ReopenedNodesLookup>();
        loads.Switch(static _ => { }, static _ => { }, lookups.Add, static _ => { }, static _ => { });
        return lookups;
    }

    /// <summary>The decompiles among <paramref name="loads"/>.</summary>
    private static List<DecompileTicket> ListDecompiles(TabLoads loads)
    {
        var decompiles = new List<DecompileTicket>();
        loads.Switch(decompiles.Add, static _ => { }, static _ => { }, static _ => { }, static _ => { });
        return decompiles;
    }

    [Fact]
    public async Task OpenAssembliesAsync_FileOpened_ShowsItsRoot()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(d.Fixture("Greeter"));
        var fixtures = FixturesOf(d);
        var ports = new FakeAssemblyOpeningPorts { Opened = new Outcome<LoadedAssembly?>.Ok(fixtures) };
        using var shutdown = new CancellationTokenSource();
        var path = PathTo("Opened.dll");

        var load = await new AssemblyOpening(ports).OpenAssembliesAsync(d.AppState, [path], shutdown.Token);

        Assert.Equal(path, Assert.Single(Assert.Single(ports.OpenedPaths)));
        Assert.Equal(d.AppState.Opens.Token, Assert.Single(ports.Stops));
        Assert.Equal(shutdown.Token, Assert.Single(ports.Tokens));
        Assert.Same(BrowseSessions.EnsureRoot(d.AppState.Session, fixtures), d.AppState.Navigation.ActiveTab?.Node);
        Assert.Empty(ports.Reported);
        var active = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(active);
        Assert.Same(active, load?.Tab);
    }

    [Fact]
    public async Task OpenAssembliesAsync_OpeningFailedOrCancelled_ReportsOnlyTheFailure()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var shown = d.AppState.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var error = new IOException("unreadable");
        var failed = new FakeAssemblyOpeningPorts { Opened = new Outcome<LoadedAssembly?>.Failed(error) };
        var cancelled = new FakeAssemblyOpeningPorts { Opened = new Outcome<LoadedAssembly?>.Failed(new OperationCanceledException()) };

        var failedLoad = await new AssemblyOpening(failed).OpenAssembliesAsync(d.AppState, [PathTo("Failing.dll")], CancellationToken.None);
        var cancelledLoad = await new AssemblyOpening(cancelled).OpenAssembliesAsync(d.AppState, [PathTo("Cancelled.dll")], CancellationToken.None);

        Assert.Same(error, Assert.IsType<AppProblem.OpenFailed>(Assert.Single(failed.Reported)).Error);
        Assert.Empty(cancelled.Reported);
        Assert.Same(shown, d.AppState.Navigation.ActiveTab?.Node);
        Assert.Null(failedLoad);
        Assert.Null(cancelledLoad);
    }

    [Fact]
    public async Task OpenAssembliesAsync_SomethingElseAskedForMeanwhile_DoesNotShowIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(d.Fixture("Greeter"));
        var shown = d.AppState.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var appState = d.AppState;
        var ports = new FakeAssemblyOpeningPorts { Opened = new Outcome<LoadedAssembly?>.Ok(FixturesOf(d)), WhileWorking = () => appState.Navigation.BeginRequest() };

        var load = await new AssemblyOpening(ports).OpenAssembliesAsync(appState, [PathTo("Opened.dll")], CancellationToken.None);

        Assert.Same(shown, appState.Navigation.ActiveTab?.Node);
        Assert.Empty(ports.Reported);
        Assert.Null(load);
    }

    [Fact]
    public async Task OpenAssembliesAsync_NoFiles_OpensNothing()
    {
        var d = AppStateDriver.WithoutScreen();
        var ports = new FakeAssemblyOpeningPorts();

        var load = await new AssemblyOpening(ports).OpenAssembliesAsync(d.AppState, [], CancellationToken.None);

        Assert.Empty(ports.Asked);
        Assert.Null(load);
    }

    [Fact]
    public async Task OpenDroppedFilesAsync_FoldersAmongThem_OpensOnlyTheFiles()
    {
        var d = AppStateDriver.WithoutScreen();
        var file = PathTo("Dropped.dll");
        var folder = PathTo("Folder");
        var ports = new FakeAssemblyOpeningPorts { Files = [file.Text] };

        var load = await new AssemblyOpening(ports).OpenDroppedFilesAsync(d.AppState, [folder.Text, file.Text], CancellationToken.None);

        Assert.Equal(file, Assert.Single(Assert.Single(ports.OpenedPaths)));
        Assert.Null(load);
    }

    [Fact]
    public async Task OpenDependenciesAsync_OpeningFailedOrCancelled_ReportsOnlyTheFailure()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var fixtures = FixturesOf(d);
        var error = new IOException("unreadable");
        var failed = new FakeAssemblyOpeningPorts { ReferencesOpened = new Outcome<List<LoadedModule>>.Failed(error) };
        var cancelled = new FakeAssemblyOpeningPorts { ReferencesOpened = new Outcome<List<LoadedModule>>.Failed(new OperationCanceledException()) };

        await new AssemblyOpening(failed).OpenDependenciesAsync(d.AppState.Opens, fixtures, CancellationToken.None);
        await new AssemblyOpening(cancelled).OpenDependenciesAsync(d.AppState.Opens, fixtures, CancellationToken.None);

        Assert.Equal(d.AppState.Opens.Token, Assert.Single(failed.Stops));
        Assert.Same(error, Assert.IsType<AppProblem.OpenFailed>(Assert.Single(failed.Reported)).Error);
        Assert.Empty(cancelled.Reported);
    }

    [Fact]
    public async Task UsePdbAsync_PdbOfAnotherBuildOrCheckFailed_ReportsItAndPicksNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var fixtures = FixturesOf(d);
        var otherBuild = new FakeAssemblyReloadingPorts { PdbChecked = new Outcome<PdbCheck>.Ok(PdbCheck.OtherBuild) };
        var failed = new FakeAssemblyReloadingPorts { PdbChecked = new Outcome<PdbCheck>.Failed(new IOException("unreadable")) };

        var otherBuildLoads = await new AssemblyReloading(otherBuild).UsePdbAsync(d.AppState, fixtures, "other.pdb", CancellationToken.None);
        var failedLoads = await new AssemblyReloading(failed).UsePdbAsync(d.AppState, fixtures, "broken.pdb", CancellationToken.None);

        var unusable = Assert.IsType<AppProblem.PdbUnusable>(Assert.Single(otherBuild.Reported));
        Assert.Equal(PdbCheck.OtherBuild, unusable.Check);
        Assert.Equal("other.pdb", unusable.PdbPath);
        Assert.Equal(PdbCheck.Unreadable, Assert.IsType<AppProblem.PdbUnusable>(Assert.Single(failed.Reported)).Check);
        Assert.DoesNotContain(nameof(IPorts.ReadAgainAsync), otherBuild.Asked);
        Assert.DoesNotContain(nameof(IPorts.ReadAgainAsync), failed.Asked);
        Assert.Empty(d.AppState.Assemblies.Pdbs.All);
        Assert.Empty(otherBuildLoads);
        Assert.Empty(failedLoads);
    }

    [Fact]
    public async Task UsePdbAsync_PdbMatchesButReadingAgainFails_PicksItAndReportsTheAssemblyNotReloaded()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var fixtures = FixturesOf(d);
        var error = new IOException("unreadable");
        var ports = new FakeAssemblyReloadingPorts { ReadAgain = (_, _) => new Outcome<ReopenedFile>.Failed(error) };
        using var shutdown = new CancellationTokenSource();

        var loads = await new AssemblyReloading(ports).UsePdbAsync(d.AppState, fixtures, "matching.pdb", shutdown.Token);

        Assert.Equal("matching.pdb", d.AppState.Assemblies.Pdbs.All[fixtures.FilePath]);
        Assert.Equal([nameof(IPorts.CheckPdbAsync), nameof(IPorts.ReadAgainAsync)], ports.Asked);
        Assert.All(ports.Tokens, t => Assert.Equal(shutdown.Token, t));
        var notReloaded = Assert.IsType<AppProblem.AssemblyNotReloaded>(Assert.Single(ports.Reported));
        Assert.Same(error, notReloaded.Error);
        Assert.False(d.AppState.IsReopening);
        Assert.Empty(loads);
    }

    [Fact]
    public async Task SetTargetFrameworkAsync_FrameworkInUse_ReadsNothingAgain()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var ports = new FakeAssemblyReloadingPorts();

        var loads = await new AssemblyReloading(ports).SetTargetFrameworkAsync(d.AppState, FixturesOf(d), null, CancellationToken.None);

        Assert.Empty(ports.Asked);
        Assert.Empty(d.AppState.Assemblies.TargetFrameworks.All);
        Assert.Empty(loads);
    }

    [Fact]
    public async Task SetTargetFrameworkAsync_OtherFramework_ListsTheAssemblyReadAgainAndShowsTheTabsPlacesInIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var fixtures = FixturesOf(d);
        var tab = appState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var ports = new FakeAssemblyReloadingPorts { ReadAgain = (item, places) => ReadAgain(appState, item, places) };

        var loads = await new AssemblyReloading(ports).SetTargetFrameworkAsync(appState, fixtures, Frameworks.Net48, CancellationToken.None);

        var fresh = Assert.Single(appState.Assemblies.Assemblies, a => a.FilePath.Text == AppStateDriver.FixturePath);
        Assert.NotSame(fixtures, fresh);
        Assert.Equal(Frameworks.Net48, appState.Assemblies.TargetFrameworks.All[fixtures.FilePath]);
        Assert.Same(BrowseSessions.EnsureRoot(appState.Session, fresh), tab.Node);
        Assert.False(appState.IsReopening);
        Assert.Empty(ports.Reported);
        Assert.Same(tab, Assert.Single(loads).Tab);
    }

    private static Outcome<ReopenedFile> ReadAgain(AppState appState, LoadedAssembly item, IReadOnlyList<NodePath> places)
    {
        var fresh = AssemblyListing.ReadAgain(appState.Assemblies, item);
        var found = places
            .Select(p => BrowseSessions.FindNode(appState.Session, fresh, p))
            .ToArray();
        return new Outcome<ReopenedFile>.Ok(new ReopenedFile { Item = fresh, Places = found });
    }

    [Fact]
    public async Task SetTargetFrameworkAsync_ReopenedAgainMeanwhile_NeitherListsNorReports()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var fixtures = FixturesOf(d);
        var ports = new FakeAssemblyReloadingPorts
        {
            ReadAgain = (_, _) => new Outcome<ReopenedFile>.Failed(new IOException("unreadable")),
            WhileWorking = () => appState.Reopens.Start(fixtures.FilePath),
        };

        var loads = await new AssemblyReloading(ports).SetTargetFrameworkAsync(appState, fixtures, Frameworks.Net48, CancellationToken.None);

        Assert.Same(fixtures, FixturesOf(d));
        Assert.Empty(ports.Reported);
        Assert.Empty(loads);
    }

    [Fact]
    public async Task RefreshAssembliesAsync_TabsPlacesGone_ShowsThemGoneAndReportsEach()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var tab = appState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var location = tab.Location;
        Assert.NotNull(location);
        var ports = new FakeAssemblyReloadingPorts { Reopen = static places => new Outcome<ReloadedNodes>.Ok(new ReloadedNodes { Expanded = [], Places = new BrowseNode?[places.Count] }) };
        using var shutdown = new CancellationTokenSource();

        var loads = await new AssemblyReloading(ports).RefreshAssembliesAsync(appState, [], shutdown.Token);

        Assert.Equal(AppStateDriver.FixturePath, Assert.Single(Assert.Single(ports.OpenedPaths)).Text);
        Assert.Equal(shutdown.Token, Assert.Single(ports.Tokens));
        Assert.Equal(location, Assert.IsType<AppProblem.GoneAfterReload>(Assert.Single(ports.Reported)).Location);
        Assert.Null(tab.Node);
        Assert.False(appState.IsReloading);
        Assert.Empty(ListDecompiles(loads));
    }

    [Fact]
    public async Task RefreshAssembliesAsync_Reloaded_LeavesSavingToTheAutosave()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        appState.SettingsSaving.Save(appState);
        var ports = new FakeAssemblyReloadingPorts { Reopen = static places => new Outcome<ReloadedNodes>.Ok(new ReloadedNodes { Expanded = [], Places = new BrowseNode?[places.Count] }) };

        var loads = await new AssemblyReloading(ports).RefreshAssembliesAsync(appState, [], CancellationToken.None);

        Assert.Empty(ListDecompiles(loads));
        Assert.False(appState.IsReloading);
        Assert.NotEqual(SettingArea.Settings.CaptureSavedSources(appState), appState.LastSaved);
        Assert.IsType<Saving.Saved>(appState.SettingsSaving.SaveIfChanged(appState, d.Now));
    }

    [Fact]
    public async Task RefreshAssembliesAsync_ReopeningFailed_ReportsIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var error = new IOException("unreadable");
        var ports = new FakeAssemblyReloadingPorts { Reopen = _ => new Outcome<ReloadedNodes>.Failed(error) };

        var loads = await new AssemblyReloading(ports).RefreshAssembliesAsync(d.AppState, [], CancellationToken.None);

        Assert.Same(error, Assert.IsType<AppProblem.AssembliesNotReloaded>(Assert.Single(ports.Reported)).Error);
        Assert.False(d.AppState.IsReloading);
        Assert.Empty(ListDecompiles(loads));
    }

    [Fact]
    public async Task RefreshAssembliesAsync_ListClearedMeanwhile_StopsTheReloadAndReportsNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var ports = new FakeAssemblyReloadingPorts
        {
            Reopen = _ => new Outcome<ReloadedNodes>.Failed(new OperationCanceledException()),
            WhileWorking = () => d.Change(static a => CloseAllAssemblies(a.ListSession)),
        };

        var loads = await new AssemblyReloading(ports).RefreshAssembliesAsync(appState, [], CancellationToken.None);

        Assert.True(Assert.Single(ports.Stops).IsCancellationRequested);
        Assert.Empty(ports.Reported);
        Assert.False(appState.IsReloading);
        Assert.Empty(ListDecompiles(loads));
    }

    [Fact]
    public async Task HandleAsync_OpeningAsReloadFailed_ReportsIt()
    {
        var d = AppStateDriver.WithoutScreen();
        var error = new IOException("unreadable");
        var ports = new FakeOpenAssembliesAsReloadHandlerPorts { Opened = new Outcome<LoadedAssembly?>.Failed(error) };

        var loads = await new OpenAssembliesAsReloadHandler(ports).HandleAsync(d.AppState.ListSession, [PathTo("Listed.dll")], d.AppState.Reload.Start(), CancellationToken.None);

        Assert.Same(error, Assert.IsType<AppProblem.OpenFailed>(Assert.Single(ports.Reported)).Error);
        Assert.False(d.AppState.IsReloading);
        Assert.Empty(ListDecompiles(loads));
    }

    [Fact]
    public async Task HandleAsync_OpenedAsReload_LeavesSavingWhatChangedToTheAutosave()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        appState.SettingsSaving.Save(appState);
        appState.Locale = Locale.De;
        var ports = new FakeOpenAssembliesAsReloadHandlerPorts { Opened = new Outcome<LoadedAssembly?>.Ok(FixturesOf(d)) };

        var loads = await new OpenAssembliesAsReloadHandler(ports).HandleAsync(appState.ListSession, [PathTo("Listed.dll")], appState.Reload.Start(), CancellationToken.None);

        Assert.Empty(ListDecompiles(loads));
        Assert.False(appState.IsReloading);
        Assert.NotEqual(SettingArea.Settings.CaptureSavedSources(appState), appState.LastSaved);
        Assert.IsType<Saving.Saved>(appState.SettingsSaving.SaveIfChanged(appState, d.Now));
    }

    [Fact]
    public async Task HandleAsync_OpeningAsReloadFailed_LeavesSavingWhatChangedToTheAutosave()
    {
        var d = AppStateDriver.WithoutScreen();
        var appState = d.AppState;
        appState.SettingsSaving.Save(appState);
        appState.Locale = Locale.De;
        var ports = new FakeOpenAssembliesAsReloadHandlerPorts { Opened = new Outcome<LoadedAssembly?>.Failed(new IOException("unreadable")) };

        var loads = await new OpenAssembliesAsReloadHandler(ports).HandleAsync(appState.ListSession, [PathTo("Listed.dll")], appState.Reload.Start(), CancellationToken.None);

        Assert.Empty(ListDecompiles(loads));
        Assert.False(appState.IsReloading);
        Assert.IsType<Saving.Saved>(appState.SettingsSaving.SaveIfChanged(appState, d.Now));
    }

    [Fact]
    public async Task HandleAsync_TabsWaitingForNodes_HandsBackTheLookupOfThemUnstarted()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var place = BrowseNodes.GetPath(d.Fixture("Greeter"));
        var record = new NodeDocumentRecord("document-9", "Greeter", place, DecompilationLanguage.CSharp, null);
        var tab = d.AppState.Navigation.OpenReopenedTab();
        Tabs.WaitForNode(tab, place, record.Title, record.Language);
        var reopened = SavedDocuments.WaitFor(d.AppState.Reopened, tab, record);
        var ports = new FakeOpenAssembliesAsReloadHandlerPorts { Opened = new Outcome<LoadedAssembly?>.Failed(new IOException("unreadable")) };

        var loads = await new OpenAssembliesAsReloadHandler(ports).HandleAsync(d.AppState.ListSession, [PathTo("Listed.dll")], d.AppState.Reload.Start(), CancellationToken.None);

        var waiting = Assert.Single(Assert.Single(ListLookups(loads)).Nodes);
        Assert.Same(reopened, waiting.Reopened);
        Assert.Equal(place, waiting.Place);
        Assert.False(d.AppState.IsFindingDocuments);
        Assert.IsType<TabContent.Reloading>(tab.Content);
        Assert.Empty(ListDecompiles(loads));
    }

    [Fact]
    public async Task HandleAsync_AnotherReloadStartedWhileOpeningAsReload_ReportsNothing()
    {
        var d = AppStateDriver.WithoutScreen();
        var appState = d.AppState;
        var ports = new FakeOpenAssembliesAsReloadHandlerPorts
        {
            Opened = new Outcome<LoadedAssembly?>.Failed(new OperationCanceledException()),
#pragma warning disable RL0807 // a newer reload only supersedes this one here: nothing runs under its ticket
            WhileWorking = () => appState.Reload.Start(),
#pragma warning restore RL0807
        };

        var loads = await new OpenAssembliesAsReloadHandler(ports).HandleAsync(appState.ListSession, [PathTo("Listed.dll")], appState.Reload.Start(), CancellationToken.None);

        Assert.True(Assert.Single(ports.Stops).IsCancellationRequested);
        Assert.Empty(ports.Reported);
        Assert.Same(TabLoads.None, loads);
    }
}
