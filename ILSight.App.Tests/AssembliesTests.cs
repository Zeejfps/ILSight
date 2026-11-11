using ILSight.Core;
using static ILSight.Assemblies;

namespace ILSight.App.Tests;

public sealed class AssembliesTests
{
    /// <summary>Answers each port member with what the test set, doing <see cref="WhileWorking"/> before it answers;
    /// keeps what was asked, the tokens it was asked with, and the problems reported.</summary>
    private sealed class FakePorts : IPorts
    {
        public Outcome<LoadedAssembly?> Opened { get; init; } = new Outcome<LoadedAssembly?>.Ok(null);

        public Outcome<List<LoadedModule>> ReferencesOpened { get; init; } = new Outcome<List<LoadedModule>>.Ok([]);

        public Outcome<PdbCheck> PdbChecked { get; init; } = new Outcome<PdbCheck>.Ok(PdbCheck.Matches);

        public Func<LoadedAssembly, IReadOnlyList<NodePath>, Outcome<Reopened>> ReadAgain { get; init; } =
            static (_, _) => new Outcome<Reopened>.Failed(new IOException("Reading again was not expected."));

        public Func<IReadOnlyList<NodePath>, Outcome<Reloaded>> Reopen { get; init; } =
            static _ => new Outcome<Reloaded>.Failed(new IOException("Reopening was not expected."));

        public IReadOnlyCollection<string> Files { get; init; } = [];

        public Action WhileWorking { get; init; } = static () => { };

        public List<string> Asked { get; } = [];

        public List<IReadOnlyList<AssemblyPath>> OpenedPaths { get; } = [];

        public List<CancellationToken> Stops { get; } = [];

        public List<CancellationToken> Tokens { get; } = [];

        public List<AppProblem> Reported { get; } = [];

        public void ReportError(AppProblem problem)
        {
            Reported.Add(problem);
        }

        public bool IsFile(string path)
        {
            return Files.Contains(path);
        }

        public Task<Outcome<LoadedAssembly?>> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, CancellationToken stop, CancellationToken ct)
        {
            OpenedPaths.Add(paths);
            Stops.Add(stop);
            return AnswerAsync(nameof(OpenFilesAsync), Opened, ct);
        }

        public Task<Outcome<List<LoadedModule>>> OpenReferencesAsync(LoadedModule assembly, CancellationToken stop, CancellationToken ct)
        {
            Stops.Add(stop);
            return AnswerAsync(nameof(OpenReferencesAsync), ReferencesOpened, ct);
        }

        public Task<Outcome<PdbCheck>> CheckPdbAsync(LoadedModule assembly, string pdbPath, CancellationToken ct)
        {
            return AnswerAsync(nameof(CheckPdbAsync), PdbChecked, ct);
        }

        public Task<Outcome<Reopened>> ReadAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct)
        {
            return AnswerAsync(nameof(ReadAgainAsync), ReadAgain(item, places), ct);
        }

        public Task<Outcome<Reloaded>> ReopenFilesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded, IReadOnlyList<NodePath> places,
            CancellationToken stop, CancellationToken ct)
        {
            OpenedPaths.Add(paths);
            Stops.Add(stop);
            return AnswerAsync(nameof(ReopenFilesAsync), Reopen(places), ct);
        }

        private Task<T> AnswerAsync<T>(string member, T answer, CancellationToken ct)
        {
            Asked.Add(member);
            Tokens.Add(ct);
            WhileWorking();
            return Task.FromResult(answer);
        }
    }

    private static LoadedModule FixturesOf(AppStateDriver d)
    {
        return d.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
    }

    private static AssemblyPath PathTo(string file)
    {
        return Assert.IsType<AssemblyPath>(AssemblyPath.Parse(Path.Combine(Path.GetTempPath(), file)));
    }

    [Fact]
    public async Task OpenAssembliesAsync_FileOpened_ShowsItsRoot()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(d.Fixture("Greeter"));
        var fixtures = FixturesOf(d);
        var ports = new FakePorts { Opened = new Outcome<LoadedAssembly?>.Ok(fixtures) };
        using var shutdown = new CancellationTokenSource();
        var path = PathTo("Opened.dll");

        await OpenAssembliesAsync(d.AppState, [path], ports, shutdown.Token);

        Assert.Equal(path, Assert.Single(Assert.Single(ports.OpenedPaths)));
        Assert.Equal(d.AppState.Opens.Token, Assert.Single(ports.Stops));
        Assert.Equal(shutdown.Token, Assert.Single(ports.Tokens));
        Assert.Same(BrowseSessions.EnsureRoot(d.AppState.Session, fixtures), d.AppState.Tabs.Active?.Node);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task OpenAssembliesAsync_OpeningFailedOrCancelled_ReportsOnlyTheFailure()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var shown = d.AppState.Tabs.Active?.Node;
        var error = new IOException("unreadable");
        var failed = new FakePorts { Opened = new Outcome<LoadedAssembly?>.Failed(error) };
        var cancelled = new FakePorts { Opened = new Outcome<LoadedAssembly?>.Failed(new OperationCanceledException()) };

        await OpenAssembliesAsync(d.AppState, [PathTo("Failing.dll")], failed, CancellationToken.None);
        await OpenAssembliesAsync(d.AppState, [PathTo("Cancelled.dll")], cancelled, CancellationToken.None);

        Assert.Same(error, Assert.IsType<AppProblem.OpenFailed>(Assert.Single(failed.Reported)).Error);
        Assert.Empty(cancelled.Reported);
        Assert.Same(shown, d.AppState.Tabs.Active?.Node);
    }

    [Fact]
    public async Task OpenAssembliesAsync_SomethingElseAskedForMeanwhile_DoesNotShowIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(d.Fixture("Greeter"));
        var shown = d.AppState.Tabs.Active?.Node;
        var appState = d.AppState;
        var ports = new FakePorts { Opened = new Outcome<LoadedAssembly?>.Ok(FixturesOf(d)), WhileWorking = () => appState.Intent.Begin() };

        await OpenAssembliesAsync(appState, [PathTo("Opened.dll")], ports, CancellationToken.None);

        Assert.Same(shown, appState.Tabs.Active?.Node);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task OpenAssembliesAsync_NoFiles_OpensNothing()
    {
        var d = AppStateDriver.WithoutScreen();
        var ports = new FakePorts();

        await OpenAssembliesAsync(d.AppState, [], ports, CancellationToken.None);

        Assert.Empty(ports.Asked);
    }

    [Fact]
    public async Task OpenDroppedFilesAsync_FoldersAmongThem_OpensOnlyTheFiles()
    {
        var d = AppStateDriver.WithoutScreen();
        var file = PathTo("Dropped.dll");
        var folder = PathTo("Folder");
        var ports = new FakePorts { Files = [file.Text] };

        await OpenDroppedFilesAsync(d.AppState, [folder.Text, file.Text], ports, CancellationToken.None);

        Assert.Equal(file, Assert.Single(Assert.Single(ports.OpenedPaths)));
    }

    [Fact]
    public async Task OpenDependenciesAsync_OpeningFailedOrCancelled_ReportsOnlyTheFailure()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var fixtures = FixturesOf(d);
        var error = new IOException("unreadable");
        var failed = new FakePorts { ReferencesOpened = new Outcome<List<LoadedModule>>.Failed(error) };
        var cancelled = new FakePorts { ReferencesOpened = new Outcome<List<LoadedModule>>.Failed(new OperationCanceledException()) };

        await OpenDependenciesAsync(d.AppState.Opens, fixtures, failed, CancellationToken.None);
        await OpenDependenciesAsync(d.AppState.Opens, fixtures, cancelled, CancellationToken.None);

        Assert.Equal(d.AppState.Opens.Token, Assert.Single(failed.Stops));
        Assert.Same(error, Assert.IsType<AppProblem.OpenFailed>(Assert.Single(failed.Reported)).Error);
        Assert.Empty(cancelled.Reported);
    }

    [Fact]
    public async Task UsePdbAsync_PdbOfAnotherBuildOrCheckFailed_ReportsItAndPicksNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var fixtures = FixturesOf(d);
        var otherBuild = new FakePorts { PdbChecked = new Outcome<PdbCheck>.Ok(PdbCheck.OtherBuild) };
        var failed = new FakePorts { PdbChecked = new Outcome<PdbCheck>.Failed(new IOException("unreadable")) };

        await UsePdbAsync(d.AppState, fixtures, "other.pdb", otherBuild, CancellationToken.None);
        await UsePdbAsync(d.AppState, fixtures, "broken.pdb", failed, CancellationToken.None);

        var unusable = Assert.IsType<AppProblem.PdbUnusable>(Assert.Single(otherBuild.Reported));
        Assert.Equal(PdbCheck.OtherBuild, unusable.Check);
        Assert.Equal("other.pdb", unusable.PdbPath);
        Assert.Equal(PdbCheck.Unreadable, Assert.IsType<AppProblem.PdbUnusable>(Assert.Single(failed.Reported)).Check);
        Assert.DoesNotContain(nameof(IPorts.ReadAgainAsync), otherBuild.Asked);
        Assert.DoesNotContain(nameof(IPorts.ReadAgainAsync), failed.Asked);
        Assert.Empty(d.AppState.Assemblies.Pdbs.All);
    }

    [Fact]
    public async Task UsePdbAsync_PdbMatchesButReadingAgainFails_PicksItAndReportsTheAssemblyNotReloaded()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var fixtures = FixturesOf(d);
        var error = new IOException("unreadable");
        var ports = new FakePorts { ReadAgain = (_, _) => new Outcome<Reopened>.Failed(error) };
        using var shutdown = new CancellationTokenSource();

        await UsePdbAsync(d.AppState, fixtures, "matching.pdb", ports, shutdown.Token);

        Assert.Equal("matching.pdb", d.AppState.Assemblies.Pdbs.All[fixtures.FilePath]);
        Assert.Equal([nameof(IPorts.CheckPdbAsync), nameof(IPorts.ReadAgainAsync)], ports.Asked);
        Assert.All(ports.Tokens, t => Assert.Equal(shutdown.Token, t));
        var notReloaded = Assert.IsType<AppProblem.AssemblyNotReloaded>(Assert.Single(ports.Reported));
        Assert.Same(error, notReloaded.Error);
        Assert.False(d.AppState.IsReopening);
    }

    [Fact]
    public async Task SetTargetFrameworkAsync_FrameworkInUse_ReadsNothingAgain()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var ports = new FakePorts();

        await SetTargetFrameworkAsync(d.AppState, FixturesOf(d), null, ports, CancellationToken.None);

        Assert.Empty(ports.Asked);
        Assert.Empty(d.AppState.Assemblies.TargetFrameworks.All);
    }

    [Fact]
    public async Task SetTargetFrameworkAsync_OtherFramework_ListsTheAssemblyReadAgainAndShowsTheTabsPlacesInIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var fixtures = FixturesOf(d);
        var tab = appState.Tabs.Active;
        Assert.NotNull(tab);
        var ports = new FakePorts { ReadAgain = (item, places) => ReadAgain(appState, item, places) };

        await SetTargetFrameworkAsync(appState, fixtures, Frameworks.Net48, ports, CancellationToken.None);

        var fresh = Assert.Single(appState.Assemblies.Assemblies, a => a.FilePath.Text == AppStateDriver.FixturePath);
        Assert.NotSame(fixtures, fresh);
        Assert.Equal(Frameworks.Net48, appState.Assemblies.TargetFrameworks.All[fixtures.FilePath]);
        Assert.Same(BrowseSessions.EnsureRoot(appState.Session, fresh), tab.Node);
        Assert.False(appState.IsReopening);
        Assert.Empty(ports.Reported);
    }

    private static Outcome<Reopened> ReadAgain(AppState appState, LoadedAssembly item, IReadOnlyList<NodePath> places)
    {
        var fresh = AssemblyListing.ReadAgain(appState.Assemblies, item);
        var found = places
            .Select(p => BrowseSessions.FindNode(appState.Session, fresh, p))
            .ToArray();
        return new Outcome<Reopened>.Ok(new Reopened { Item = fresh, Places = found });
    }

    [Fact]
    public async Task SetTargetFrameworkAsync_ReopenedAgainMeanwhile_NeitherListsNorReports()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var fixtures = FixturesOf(d);
        var ports = new FakePorts
        {
            ReadAgain = (_, _) => new Outcome<Reopened>.Failed(new IOException("unreadable")),
            WhileWorking = () => appState.Reopens.Start(fixtures.FilePath),
        };

        await SetTargetFrameworkAsync(appState, fixtures, Frameworks.Net48, ports, CancellationToken.None);

        Assert.Same(fixtures, FixturesOf(d));
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task RefreshAssembliesAsync_TabsPlacesGone_ShowsThemGoneAndReportsEach()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var tab = appState.Tabs.Active;
        Assert.NotNull(tab);
        var location = tab.Location;
        Assert.NotNull(location);
        var ports = new FakePorts { Reopen = static places => new Outcome<Reloaded>.Ok(new Reloaded { Expanded = [], Places = new BrowseNode?[places.Count] }) };
        using var shutdown = new CancellationTokenSource();

        await RefreshAssembliesAsync(appState, [], ports, shutdown.Token);

        Assert.Equal(AppStateDriver.FixturePath, Assert.Single(Assert.Single(ports.OpenedPaths)).Text);
        Assert.Equal(shutdown.Token, Assert.Single(ports.Tokens));
        Assert.Equal(location, Assert.IsType<AppProblem.GoneAfterReload>(Assert.Single(ports.Reported)).Location);
        Assert.Null(tab.Node);
        Assert.False(appState.IsReloading);
    }

    [Fact]
    public async Task RefreshAssembliesAsync_ReopeningFailed_ReportsIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var error = new IOException("unreadable");
        var ports = new FakePorts { Reopen = _ => new Outcome<Reloaded>.Failed(error) };

        await RefreshAssembliesAsync(d.AppState, [], ports, CancellationToken.None);

        Assert.Same(error, Assert.IsType<AppProblem.AssembliesNotReloaded>(Assert.Single(ports.Reported)).Error);
        Assert.False(d.AppState.IsReloading);
    }

    [Fact]
    public async Task RefreshAssembliesAsync_ListClearedMeanwhile_StopsTheReloadAndReportsNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var ports = new FakePorts
        {
            Reopen = _ => new Outcome<Reloaded>.Failed(new OperationCanceledException()),
            WhileWorking = () => CloseAllAssemblies(appState),
        };

        await RefreshAssembliesAsync(appState, [], ports, CancellationToken.None);

        Assert.True(Assert.Single(ports.Stops).IsCancellationRequested);
        Assert.Empty(ports.Reported);
        Assert.False(appState.IsReloading);
    }

    [Fact]
    public async Task OpenAssembliesAsReloadAsync_OpeningFailed_ReportsIt()
    {
        var d = AppStateDriver.WithoutScreen();
        var error = new IOException("unreadable");
        var ports = new FakePorts { Opened = new Outcome<LoadedAssembly?>.Failed(error) };

        await OpenAssembliesAsReloadAsync(d.AppState, [PathTo("Listed.dll")], ports, CancellationToken.None);

        Assert.Same(error, Assert.IsType<AppProblem.OpenFailed>(Assert.Single(ports.Reported)).Error);
        Assert.False(d.AppState.IsReloading);
    }

    [Fact]
    public async Task OpenAssembliesAsReloadAsync_AnotherReloadStartedMeanwhile_ReportsNothing()
    {
        var d = AppStateDriver.WithoutScreen();
        var appState = d.AppState;
        var ports = new FakePorts
        {
            Opened = new Outcome<LoadedAssembly?>.Failed(new OperationCanceledException()),
            WhileWorking = () => appState.Reload.Start(),
        };

        await OpenAssembliesAsReloadAsync(appState, [PathTo("Listed.dll")], ports, CancellationToken.None);

        Assert.True(Assert.Single(ports.Stops).IsCancellationRequested);
        Assert.Empty(ports.Reported);
    }
}
