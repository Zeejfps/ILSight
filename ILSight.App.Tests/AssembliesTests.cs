using ILSight.Core;
using ILSight.AssemblyArea;
using ILSight.SettingArea;
using ILSight.CommandArea;
using ILSight.AssemblyListArea;

namespace ILSight.App.Tests;

public sealed class AssembliesTests
{
    private static LoadedModule FixturesOf(AppStateDriver d)
    {
        return d.AppState.ListSession.Assemblies.Assemblies.Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
    }

    private static AssemblyPath PathTo(string file)
    {
        return Assert.IsType<AssemblyPath>(AssemblyPath.Parse(Path.Combine(Path.GetTempPath(), file)));
    }

    private static List<ReopenedNodesLookup> ListLookups(TabLoads loads)
    {
        var lookups = new List<ReopenedNodesLookup>();
        loads.Switch(static _ => { }, static _ => { }, lookups.Add, static _ => { }, static _ => { });
        return lookups;
    }

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
        d.Show(await d.FixtureAsync("Greeter"));
        var fixtures = FixturesOf(d);
        var ports = new FakeAssemblyOpeningPorts { Opened = await BrowseSessions.EnsureRootAsync(d.AppState.Session, fixtures) };
        using var shutdown = new CancellationTokenSource();
        var path = PathTo("Opened.dll");

        var load = await d.RunAsync(() => new AssemblyOpening(ports).OpenAssembliesAsync(d.AppState, [path], shutdown.Token));

        Assert.Equal(path, Assert.Single(Assert.Single(ports.OpenedPaths)));
        Assert.Equal(shutdown.Token, Assert.Single(ports.Tokens));
        Assert.Same(await BrowseSessions.EnsureRootAsync(d.AppState.Session, fixtures), d.AppState.ListSession.Navigation.ActiveTab?.Node);
        Assert.Empty(ports.Reported);
        var active = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(active);
        Assert.Same(active, load?.Tab);
    }

    [Fact]
    public async Task OpenAssembliesAsync_ListClearedMeanwhile_LetsTheCancellationThroughReportingNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        // As the edge starts it, the opens' canceller linked into its token.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(appState.ListSession.Opens.Token);
        var ports = new FakeAssemblyOpeningPorts { Opened = await BrowseSessions.EnsureRootAsync(appState.Session, FixturesOf(d)), WhileWorking = () => Commands.CloseAllAssemblies(appState) };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => Assert.Null(await d.RunAsync(() => new AssemblyOpening(ports).OpenAssembliesAsync(appState, [PathTo("Cancelled.dll")], linked.Token))));

        Assert.Empty(ports.Reported);
        Assert.False(appState.ListSession.Navigation.IsOpening);
    }

    [Fact]
    public async Task OpenAssembliesAsync_SomethingElseAskedForMeanwhile_DoesNotShowIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(await d.FixtureAsync("Greeter"));
        var shown = d.AppState.ListSession.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var appState = d.AppState;
        var ports = new FakeAssemblyOpeningPorts { Opened = await BrowseSessions.EnsureRootAsync(d.AppState.Session, FixturesOf(d)), WhileWorking = () => appState.ListSession.Navigation.BeginRequest() };

        var load = await d.RunAsync(() => new AssemblyOpening(ports).OpenAssembliesAsync(appState, [PathTo("Opened.dll")], CancellationToken.None));

        Assert.Same(shown, appState.ListSession.Navigation.ActiveTab?.Node);
        Assert.Empty(ports.Reported);
        Assert.Null(load);
    }

    [Fact]
    public async Task OpenAssembliesAsync_NoFiles_OpensNothing()
    {
        var d = AppStateDriver.WithoutScreen();
        var ports = new FakeAssemblyOpeningPorts();

        var load = await d.RunAsync(() => new AssemblyOpening(ports).OpenAssembliesAsync(d.AppState, [], CancellationToken.None));

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

        var load = await d.RunAsync(() => new AssemblyOpening(ports).OpenDroppedFilesAsync(d.AppState, [folder.Text, file.Text], CancellationToken.None));

        Assert.Equal(file, Assert.Single(Assert.Single(ports.OpenedPaths)));
        Assert.Null(load);
    }

    [Fact]
    public async Task OpenDroppedFilesAsync_APathThatIsNotFull_IsRefusedSayingSoAndTheRestOpened()
    {
        var d = AppStateDriver.WithoutScreen();
        var file = PathTo("Dropped.dll");
        var ports = new FakeAssemblyOpeningPorts { Files = [file.Text, "Relative.dll"] };

        var load = await d.RunAsync(() => new AssemblyOpening(ports).OpenDroppedFilesAsync(d.AppState, ["Relative.dll", file.Text], CancellationToken.None));

        Assert.Equal(file, Assert.Single(Assert.Single(ports.OpenedPaths)));
        Assert.Equal(new Refusal.NotFullPath { Path = "Relative.dll" }, Assert.Single(ports.Refused));
        Assert.Empty(ports.Reported);
        Assert.Null(load);
    }

    [Fact]
    public async Task OpenDependenciesAsync_OpeningFailed_ReportsItAnyCancellationAmongThem()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var fixtures = FixturesOf(d);
        var error = new IOException("unreadable");
        var failed = new FakeAssemblyOpeningPorts { ReferencesOpened = new Outcome<List<LoadedModule>>.Failed(error) };
        // A cancellation nobody asked for, such as the library's timeout, is a failure too.
        var timedOut = new FakeAssemblyOpeningPorts { ReferencesOpened = new Outcome<List<LoadedModule>>.Failed(new TaskCanceledException()) };

        await d.RunAsync(() => new AssemblyOpening(failed).OpenDependenciesAsync(fixtures, CancellationToken.None));
        await d.RunAsync(() => new AssemblyOpening(timedOut).OpenDependenciesAsync(fixtures, CancellationToken.None));

        Assert.Same(error, Assert.IsType<AppProblem.OpenFailed>(Assert.Single(failed.Reported)).Error);
        Assert.IsType<TaskCanceledException>(Assert.IsType<AppProblem.OpenFailed>(Assert.Single(timedOut.Reported)).Error);
    }

    [Fact]
    public async Task UsePdbAsync_PdbOfAnotherBuildOrCheckFailed_ReportsItAndPicksNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var fixtures = FixturesOf(d);
        var otherBuild = new FakeAssemblyReloadingPorts { PdbChecked = new PdbChecking.Checked { Check = PdbCheck.OtherBuild } };
        var failed = new FakeAssemblyReloadingPorts { PdbChecked = new PdbChecking.Failed { Error = new IOException("unreadable") } };

        var otherBuildLoads = await d.RunAsync(() => new AssemblyReloading(otherBuild).UsePdbAsync(d.AppState.ListSession, d.AppState.Reopens, d.AppState.Decompiler, fixtures, "other.pdb", CancellationToken.None));
        var failedLoads = await d.RunAsync(() => new AssemblyReloading(failed).UsePdbAsync(d.AppState.ListSession, d.AppState.Reopens, d.AppState.Decompiler, fixtures, "broken.pdb", CancellationToken.None));

        var unusable = Assert.IsType<Refusal.PdbUnusable>(Assert.Single(otherBuild.Refused));
        Assert.Equal(PdbCheck.OtherBuild, unusable.Check);
        Assert.Equal("other.pdb", unusable.PdbPath);
        Assert.Empty(otherBuild.Reported);
        Assert.Equal(PdbCheck.Unreadable, Assert.IsType<AppProblem.PdbUnusable>(Assert.Single(failed.Reported)).Check);
        Assert.DoesNotContain(nameof(AssemblyReloading.IPorts.ReadAgainAsync), otherBuild.Asked);
        Assert.DoesNotContain(nameof(AssemblyReloading.IPorts.ReadAgainAsync), failed.Asked);
        Assert.Empty(d.AppState.ListSession.Assemblies.Pdbs.All);
        Assert.Empty(otherBuildLoads);
        Assert.Empty(failedLoads);
    }

    [Fact]
    public async Task UsePdbAsync_AssemblyClosedBeforeTheCheck_SaysWhyAndPicksNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var fixtures = FixturesOf(d);
        var ports = new FakeAssemblyReloadingPorts { PdbChecked = new PdbChecking.Closed { Assembly = "ILSight.Fixtures.dll" } };

        var loads = await d.RunAsync(() => new AssemblyReloading(ports).UsePdbAsync(d.AppState.ListSession, d.AppState.Reopens, d.AppState.Decompiler, fixtures, "picked.pdb", CancellationToken.None));

        Assert.Equal([new Refusal.PdbNotRead { Pdb = "picked.pdb", Assembly = "ILSight.Fixtures.dll" }], ports.Refused);
        Assert.Empty(ports.Reported);
        Assert.DoesNotContain(nameof(AssemblyReloading.IPorts.ReadAgainAsync), ports.Asked);
        Assert.Empty(d.AppState.ListSession.Assemblies.Pdbs.All);
        Assert.Empty(loads);
    }

    [Fact]
    public async Task UsePdbAsync_PdbMatchesButReadingAgainFails_PicksItAndReportsTheAssemblyNotReloaded()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var fixtures = FixturesOf(d);
        var error = new IOException("unreadable");
        var ports = new FakeAssemblyReloadingPorts { ReadAgain = (_, _) => Task.FromResult<Outcome<ReopenedFile>>(new Outcome<ReopenedFile>.Failed(error)) };
        using var shutdown = new CancellationTokenSource();

        var loads = await d.RunAsync(() => new AssemblyReloading(ports).UsePdbAsync(d.AppState.ListSession, d.AppState.Reopens, d.AppState.Decompiler, fixtures, "matching.pdb", shutdown.Token));

        Assert.Equal("matching.pdb", d.AppState.ListSession.Assemblies.Pdbs.All[fixtures.FilePath]);
        Assert.Equal([nameof(AssemblyReloading.IPorts.CheckPdbAsync), nameof(AssemblyReloading.IPorts.ReadAgainAsync)], ports.Asked);
        Assert.All(ports.Tokens, t => Assert.Equal(shutdown.Token, t));
        var notReloaded = Assert.IsType<AppProblem.AssemblyNotReloaded>(Assert.Single(ports.Reported));
        Assert.Same(error, notReloaded.Error);
        Assert.False(d.AppState.Reopens.IsRunning);
        Assert.Empty(loads);
    }

    [Fact]
    public async Task SetTargetFrameworkAsync_FrameworkInUse_ReadsNothingAgain()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var ports = new FakeAssemblyReloadingPorts();

        var loads = await d.RunAsync(() => new AssemblyReloading(ports).SetTargetFrameworkAsync(d.AppState.ListSession, d.AppState.Reopens, d.AppState.Decompiler, FixturesOf(d), null, CancellationToken.None));

        Assert.Empty(ports.Asked);
        Assert.Empty(d.AppState.ListSession.Assemblies.TargetFrameworks.All);
        Assert.Empty(loads);
    }

    [Fact]
    public async Task SetTargetFrameworkAsync_OtherFramework_ListsTheAssemblyReadAgainAndShowsTheTabsPlacesInIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var fixtures = FixturesOf(d);
        var tab = appState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var ports = new FakeAssemblyReloadingPorts { ReadAgain = (item, places) => ReadAgainAsync(appState, item, places) };

        var loads = await d.RunAsync(() => new AssemblyReloading(ports).SetTargetFrameworkAsync(appState.ListSession, appState.Reopens, appState.Decompiler, fixtures, Frameworks.Net48, CancellationToken.None));

        var fresh = Assert.Single(appState.ListSession.Assemblies.Assemblies, a => a.FilePath.Text == AppStateDriver.FixturePath);
        Assert.NotSame(fixtures, fresh);
        Assert.Equal(Frameworks.Net48, appState.ListSession.Assemblies.TargetFrameworks.All[fixtures.FilePath]);
        Assert.Same(await BrowseSessions.EnsureRootAsync(appState.Session, fresh), tab.Node);
        Assert.False(appState.Reopens.IsRunning);
        Assert.Empty(ports.Reported);
        Assert.Same(tab, Assert.Single(loads).Tab);
    }

    [Fact]
    public async Task SetTargetFrameworkAsync_LanguageSwitchedWhileReadingTheFile_ShowsTheTabsPlacesInIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var fixtures = FixturesOf(d);
        var other = appState.Decompiler.Setup.Language == DecompilationLanguage.IL ? DecompilationLanguage.CSharp : DecompilationLanguage.IL;
        var ports = new FakeAssemblyReloadingPorts
        {
            ReadAgain = (item, places) => ReadAgainAsync(appState, item, places),
            WhileWorking = () => appState.Decompiler.UseLanguage(other),
        };

        var loads = await d.RunAsync(() => new AssemblyReloading(ports).SetTargetFrameworkAsync(appState.ListSession, appState.Reopens, appState.Decompiler, fixtures,
            Frameworks.Net48, CancellationToken.None));

        Assert.Equal(other, Assert.Single(loads).Language);
    }

    private static async Task<Outcome<ReopenedFile>> ReadAgainAsync(AppState appState, LoadedAssembly item, IReadOnlyList<NodePath> places)
    {
        var fresh = AssemblyListing.ReadAgain(appState.ListSession.Assemblies, item).GetValue();
        var found = new List<Lookup<BrowseNode>>();
        foreach (var place in places)
        {
            found.Add(await BrowseSessions.FindNodeAsync(appState.Session, fresh.Item, place));
        }

        return new Outcome<ReopenedFile>.Ok(new ReopenedFile { Fresh = fresh, Places = [.. found] });
    }

    [Fact]
    public async Task SetTargetFrameworkAsync_ReopenedAgainWhileReadingTheFile_ReleasesTheFileItRead()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var fixtures = FixturesOf(d);
        FreshItem? read = null;
        var ports = new FakeAssemblyReloadingPorts
        {
            ReadAgain = async (item, places) =>
            {
                var reopened = await ReadAgainAsync(appState, item, places);
                read = Assert.IsType<Outcome<ReopenedFile>.Ok>(reopened).Value.Fresh;
                return reopened;
            },
            WhileWorking = () => appState.Reopens.Start(fixtures.FilePath),
        };

        var loads = await d.RunAsync(() => new AssemblyReloading(ports).SetTargetFrameworkAsync(appState.ListSession, appState.Reopens, appState.Decompiler, fixtures, Frameworks.Net48, CancellationToken.None));

        Assert.NotNull(read);
        Assert.Same(fixtures, FixturesOf(d));
        Assert.Equal([read.Item], Assert.Single(ports.Released).Modules);
        Assert.Empty(loads);
    }

    [Fact]
    public async Task SetTargetFrameworkAsync_ReopenedAgainMeanwhile_NeitherListsNorReports()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var fixtures = FixturesOf(d);
        var ports = new FakeAssemblyReloadingPorts
        {
            ReadAgain = (_, _) => Task.FromResult<Outcome<ReopenedFile>>(new Outcome<ReopenedFile>.Failed(new IOException("unreadable"))),
            WhileWorking = () => appState.Reopens.Start(fixtures.FilePath),
        };

        var loads = await d.RunAsync(() => new AssemblyReloading(ports).SetTargetFrameworkAsync(appState.ListSession, appState.Reopens, appState.Decompiler, fixtures, Frameworks.Net48, CancellationToken.None));

        Assert.Same(fixtures, FixturesOf(d));
        Assert.Empty(ports.Reported);
        Assert.Empty(loads);
    }

    [Fact]
    public async Task RefreshAssembliesAsync_TabsPlacesGone_ShowsThemGoneAndReportsEach()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var tab = appState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var location = tab.Location;
        Assert.NotNull(location);
        var ports = new FakeAssemblyReloadingPorts { Reopen = static places => new Outcome<ReloadedNodes>.Ok(new ReloadedNodes { Expanded = [], Places = places.Select(static _ => (Outcome<Lookup<BrowseNode>>)new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound())).ToArray() }) };
        using var shutdown = new CancellationTokenSource();

        var loads = await d.RunAsync(() => new AssemblyReloading(ports).RefreshAssembliesAsync(appState.Lists, appState.ListSession, appState.ViewCommands, appState.Decompiler, [], appState.ListSession.Reload.Start(), shutdown.Token));

        Assert.Equal(AppStateDriver.FixturePath, Assert.Single(Assert.Single(ports.OpenedPaths)).Text);
        Assert.Equal(shutdown.Token, Assert.Single(ports.Tokens));
        Assert.IsType<TabContent.Gone>(tab.Content);
        Assert.Empty(ports.Reported);
        Assert.Null(tab.Node);
        Assert.False(appState.ListSession.Reload.IsRunning);
        Assert.Empty(ListDecompiles(loads));
    }

    [Fact]
    public async Task RefreshAssembliesAsync_ATabsLookupFailed_ShowsItFailedSayingWhyAndReportsItOnce()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var tab = appState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var location = tab.Location;
        Assert.NotNull(location);
        var error = new InvalidOperationException("Unreadable.");
        var ports = new FakeAssemblyReloadingPorts { Reopen = places => new Outcome<ReloadedNodes>.Ok(new ReloadedNodes { Expanded = [], Places = places.Select(_ => (Outcome<Lookup<BrowseNode>>)new Outcome<Lookup<BrowseNode>>.Failed(error)).ToArray() }) };

        var loads = await d.RunAsync(() => new AssemblyReloading(ports).RefreshAssembliesAsync(appState.Lists, appState.ListSession, appState.ViewCommands, appState.Decompiler, [], appState.ListSession.Reload.Start(), CancellationToken.None));

        var reported = Assert.IsType<AppProblem.PlaceLookupFailed>(Assert.Single(ports.Reported));
        Assert.Same(error, reported.Error);
        Assert.Equal(location, reported.Place);
        Assert.IsType<PlaceLookupStep.ReloadingTab>(reported.Step);
        var failed = Assert.IsType<TabContent.Unreadable>(tab.Content);
        Assert.Equal("Unreadable.", failed.Why);
        Assert.Equal(location, failed.Location);
        Assert.False(appState.ListSession.Reload.IsRunning);
        Assert.Empty(ListDecompiles(loads));
    }

    [Fact]
    public async Task RefreshAssembliesAsync_Reloaded_LeavesSavingToTheAutosave()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        Assert.IsType<SettingsWriteResult.Written>(appState.SettingsSaving.Save(appState));
        var ports = new FakeAssemblyReloadingPorts { Reopen = static places => new Outcome<ReloadedNodes>.Ok(new ReloadedNodes { Expanded = [], Places = places.Select(static _ => (Outcome<Lookup<BrowseNode>>)new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound())).ToArray() }) };

        var loads = await d.RunAsync(() => new AssemblyReloading(ports).RefreshAssembliesAsync(appState.Lists, appState.ListSession, appState.ViewCommands, appState.Decompiler, [], appState.ListSession.Reload.Start(), CancellationToken.None));

        Assert.Empty(ListDecompiles(loads));
        Assert.False(appState.ListSession.Reload.IsRunning);
        Assert.NotEqual(SettingArea.Settings.CaptureSavedSources(appState), appState.LastSave.Sources);
        Assert.IsType<Saving.Saved>(appState.SettingsSaving.SaveIfChanged(appState, d.Now));
    }

    [Fact]
    public async Task RefreshAssembliesAsync_ReopeningFailed_ReportsIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var error = new IOException("unreadable");
        var ports = new FakeAssemblyReloadingPorts { Reopen = _ => new Outcome<ReloadedNodes>.Failed(error) };

        var loads = await d.RunAsync(() => new AssemblyReloading(ports).RefreshAssembliesAsync(d.AppState.Lists, d.AppState.ListSession, d.AppState.ViewCommands, d.AppState.Decompiler, [], d.AppState.ListSession.Reload.Start(), CancellationToken.None));

        Assert.Same(error, Assert.IsType<AppProblem.AssembliesNotReloaded>(Assert.Single(ports.Reported)).Error);
        Assert.False(d.AppState.ListSession.Reload.IsRunning);
        Assert.Empty(ListDecompiles(loads));
    }

    [Fact]
    public async Task RefreshAssembliesAsync_ListClearedMeanwhile_LetsTheCancellationThroughReportingNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var ports = new FakeAssemblyReloadingPorts { WhileWorking = () => Commands.CloseAllAssemblies(d.AppState) };
        var ticket = appState.ListSession.Reload.Start();
        // As the edge starts it, the reload's canceller linked into its token.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ticket.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => Assert.Empty(ListDecompiles(await d.RunAsync(() => new AssemblyReloading(ports).RefreshAssembliesAsync(appState.Lists, appState.ListSession, appState.ViewCommands, appState.Decompiler, [], ticket, linked.Token)))));

        Assert.Empty(ports.Reported);
        Assert.False(appState.ListSession.Reload.IsRunning);
    }

    [Fact]
    public async Task HandleAsync_OpenedAsReload_LeavesSavingWhatChangedToTheAutosave()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        Assert.IsType<SettingsWriteResult.Written>(appState.SettingsSaving.Save(appState));
        appState.Locale = Locale.De;
        var ports = new FakeOpenAssembliesAsReloadHandlerPorts { Opened = FixturesOf(d) };

        var loads = await d.RunAsync(() => new OpenAssembliesAsReloadHandler(ports).HandleAsync(appState.ListSession, [PathTo("Listed.dll")], appState.ListSession.Reload.Start(), CancellationToken.None));

        Assert.Empty(ListDecompiles(loads));
        Assert.False(appState.ListSession.Reload.IsRunning);
        Assert.NotEqual(SettingArea.Settings.CaptureSavedSources(appState), appState.LastSave.Sources);
        Assert.IsType<Saving.Saved>(appState.SettingsSaving.SaveIfChanged(appState, d.Now));
    }

    [Fact]
    public async Task HandleAsync_TabsWaitingForNodes_HandsBackTheLookupOfThemUnstarted()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var place = (await d.FixtureAsync("Greeter")).Path;
        var record = new NodeDocumentRecord
        {
            Panel = "document-9",
            Title = "Greeter",
            Location = place,
            Language = DecompilationLanguage.CSharp,
            View = null,
        };
        var tab = d.AppState.ListSession.Navigation.OpenReopenedTab();
        d.AppState.ListSession.Navigation.WaitForNode(tab, place, record.Title, record.Language);
        var reopened = SavedDocuments.WaitFor(d.AppState.ListSession.Reopened, tab, record);
        var ports = new FakeOpenAssembliesAsReloadHandlerPorts();

        var loads = await d.RunAsync(() => new OpenAssembliesAsReloadHandler(ports).HandleAsync(d.AppState.ListSession, [PathTo("Listed.dll")], d.AppState.ListSession.Reload.Start(), CancellationToken.None));

        var waiting = Assert.Single(Assert.Single(ListLookups(loads)).Nodes);
        Assert.Same(reopened, waiting.Reopened);
        Assert.Equal(place, waiting.Place);
        Assert.IsType<TabContent.Reloading>(tab.Content);
        Assert.Empty(ListDecompiles(loads));
    }

    [Fact]
    public async Task HandleAsync_AnotherReloadStartedWhileOpeningAsReload_LetsTheCancellationThrough()
    {
        var d = AppStateDriver.WithoutScreen();
        var appState = d.AppState;
        var ports = new FakeOpenAssembliesAsReloadHandlerPorts
        {
#pragma warning disable RL0807 // a newer reload only supersedes this one here: nothing runs under its ticket
            WhileWorking = () => appState.ListSession.Reload.Start(),
#pragma warning restore RL0807
        };
        var ticket = appState.ListSession.Reload.Start();
        // As the edge starts it, the reload's canceller linked into its token.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ticket.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => Assert.Empty(ListDecompiles(await d.RunAsync(() => new OpenAssembliesAsReloadHandler(ports).HandleAsync(appState.ListSession, [PathTo("Listed.dll")], ticket, linked.Token)))));

        Assert.True(Assert.Single(ports.Tokens).IsCancellationRequested);
    }

    [Fact]
    public async Task HandleAsync_ReopeningEndedBeforeAFileLanded_TakesItOffThoseOnTheirWay()
    {
        var d = AppStateDriver.WithoutScreen();
        var session = d.AppState.ListSession;
        d.AppState.Lists.RememberActivePaths([PathTo("Listed.dll")]);
        var reopening = session.BeginReopening(d.AppState.Lists);
        Assert.Equal([PathTo("Listed.dll")], session.Assemblies.Arriving);
        var ports = new FakeOpenAssembliesAsReloadHandlerPorts();

        var loads = await d.RunAsync(() => new OpenAssembliesAsReloadHandler(ports).HandleAsync(session, reopening.Paths, reopening.Reload, CancellationToken.None));

        Assert.Empty(ListDecompiles(loads));
        Assert.Empty(session.Assemblies.Arriving);
    }

    [Fact]
    public void Switching_lists_while_the_reopening_runs_leaves_none_of_its_files_on_their_way()
    {
        var d = AppStateDriver.WithoutScreen();
        var appState = d.AppState;
        appState.Lists.RememberActivePaths([PathTo("Listed.dll")]);
        var reopening = appState.ListSession.BeginReopening(appState.Lists);
        appState.Lists.Add("Other", [PathTo("Other.dll")]);

        var opening = AssemblyLists.SwitchAssemblyList(appState.Lists, appState.ListSession, appState.KeptScreen.View, "Other");

        Assert.NotNull(opening);
        Assert.False(appState.ListSession.TryFinishReopening(reopening.Reload));
        Assert.Equal([PathTo("Other.dll")], appState.ListSession.Assemblies.Arriving);
        Assert.Equal([PathTo("Other.dll")], opening.Reopening.Paths);
    }
}
