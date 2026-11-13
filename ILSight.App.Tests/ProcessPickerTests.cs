using ILSight.Core;
using ILSight.Core.Processes;
using ZGF.Gui.Immediate.Widgets;
using ILSight.ProcessArea;

namespace ILSight.App.Tests;

/// <summary>The "open from running process" dialog's workflows over a fake port: what each does with what the port
/// answers, and the tokens it asks with; and its adapter over this test's own process.</summary>
public sealed class ProcessPickerTests
{
    private static readonly RunningProcess Web = Process(10, "Web", "Web.Api");
    private static readonly RunningProcess Worker = Process(22, "dotnet", "Jobs.Worker");
    private static readonly RunningProcess Silent = new RunningProcess
    {
        Id = new ProcessId(31),
        Name = "Stuck",
        Info = null,
    };

    private static readonly RunningModule WebDll = new RunningModule.OnDisk
    {
        Name = "Web.Api.dll",
        Path = TestPaths.FakePath("Web.Api.dll"),
    };
    private static readonly RunningModule CoreLib = new RunningModule.OnDisk
    {
        Name = "System.Private.CoreLib.dll",
        Path = TestPaths.FakePath("System.Private.CoreLib.dll"),
    };
    private static readonly RunningModule Generated = new RunningModule.InMemory { Name = "Generated" };

    private sealed class FakePorts : ProcessBrowser.IPorts
    {
        // A test changes it as processes come and go.
        public List<RunningProcess> Running { get; init; } = [];

        public Func<CancellationToken, Task<Outcome<IReadOnlyList<RunningProcess>>>>? Scan { get; init; }

        public Func<RunningProcess, CancellationToken, Task<Outcome<ModuleScan>>> Modules { get; init; } = static (process, _) => ListedAsync(process, []);

        public List<CancellationToken> ScanTokens { get; } = [];

        public List<CancellationToken> ListingTokens { get; } = [];

        public List<AppProblem> Reported { get; } = [];

        public void ReportError(AppProblem problem)
        {
            Reported.Add(problem);
        }

        public Task<Outcome<IReadOnlyList<RunningProcess>>> ListProcessesAsync(CancellationToken ct)
        {
            ScanTokens.Add(ct);
            if (Scan is not null)
            {
                return Scan(ct);
            }

            return Task.FromResult<Outcome<IReadOnlyList<RunningProcess>>>(new Outcome<IReadOnlyList<RunningProcess>>.Ok(Running.ToList()));
        }

        public Task<Outcome<ModuleScan>> ListModulesAsync(RunningProcess process, CancellationToken ct)
        {
            ListingTokens.Add(ct);
            return Modules(process, ct);
        }
    }

    private static Task<Outcome<ModuleScan>> ListedAsync(RunningProcess process, IReadOnlyList<RunningModule> modules, AssemblyPath? entry = null)
    {
        return AnswerAsync(new ModuleScan.Loaded { Process = process, Modules = modules, EntryAssembly = entry });
    }

    private static Task<Outcome<ModuleScan>> AnswerAsync(ModuleScan scan)
    {
        return Task.FromResult<Outcome<ModuleScan>>(new Outcome<ModuleScan>.Ok(scan));
    }

    private static Task<Outcome<IReadOnlyList<RunningProcess>>> FoundAsync(params RunningProcess[] processes)
    {
        return Task.FromResult<Outcome<IReadOnlyList<RunningProcess>>>(new Outcome<IReadOnlyList<RunningProcess>>.Ok(processes));
    }

    private static RunningProcess Process(int id, string name, string entry)
    {
        return new RunningProcess
        {
            Id = new ProcessId(id),
            Name = name,
            Info = new ProcessInfo
            {
                CommandLine = null,
                Architecture = "arm64",
                EntryAssemblyName = entry,
                RuntimeVersion = "10.0.0",
            },
        };
    }

    // As the edge does, on the token of the work it was started as, which the edge links to the app's shutdown.
    private static Task ScanAsync(ProcessBrowser browser, WorkTicket scan)
    {
        return browser.ScanAsync(scan, scan.Token);
    }

    private static async Task<ProcessPicker> OpenAsync(FakePorts ports)
    {
        var picker = new ProcessPicker { Browser = new ProcessBrowser(ports) };
        await ScanAsync(picker.Browser, picker.Open());
        Assert.Null(Follow(picker));
        return picker;
    }

    private static ModuleListingTicket? Follow(ProcessPicker picker)
    {
        picker.Browser.TakeFilter(picker.Filter);
        return picker.Browser.FollowSelection();
    }

    private static void Close(ProcessPicker picker)
    {
        picker.IsOpen = false;
        picker.Browser.StopUnlessOpen(picker.IsOpen);
    }

    private static List<RunningProcess> Filter(ProcessPicker picker, string filter)
    {
        picker.Filter = filter;
        picker.Browser.TakeFilter(filter);
        return picker.Browser.Filtered.ToList();
    }

    private static ModuleListingTicket Select(ProcessPicker picker, RunningProcess process)
    {
        var filtered = picker.Browser.Filtered;
        picker.Browser.ProcessSelection.Select(filtered.ToList().IndexOf(process), filtered.Count);
        var listing = Follow(picker);
        Assert.NotNull(listing);
        Assert.Equal(process, listing.Process);
        return listing;
    }

    private static Task SelectAndListAsync(ProcessPicker picker, RunningProcess process)
    {
        var listing = Select(picker, process);
        return picker.Browser.ListModulesAsync(listing, listing.Work.Token);
    }

    [Fact]
    public async Task ScanAsync_ProcessesFound_ShowsThemAndTheFilterMatchesNameIdOrEntryAssembly()
    {
        var ports = new FakePorts { Running = [Web, Worker, Silent] };

        var picker = await OpenAsync(ports);

        Assert.Equal([Web, Worker, Silent], picker.Browser.Filtered);
        Assert.Equal([Worker], Filter(picker, "jobs"));
        Assert.Equal([Worker], Filter(picker, "22"));
        Assert.Equal([Silent], Filter(picker, "STUCK"));
        Assert.Empty(Filter(picker, "nothing"));
        Assert.True(picker.Browser.TryTakeFilterFocus());
        Assert.False(picker.Browser.TryTakeFilterFocus());
    }

    [Fact]
    public async Task ListModulesAsync_ModulesListed_ShowsThemAndPicksOutTheEntryAssembly()
    {
        var ports = new FakePorts { Running = [Web], Modules = static (process, _) => ListedAsync(process, [CoreLib, Generated, WebDll], TestPaths.FakePath("Web.Api.dll")) };
        var picker = await OpenAsync(ports);
        using var shutdown = new CancellationTokenSource();

        await picker.Browser.ListModulesAsync(Select(picker, Web), shutdown.Token);

        var loaded = Assert.IsType<ModuleScan.Loaded>(picker.Browser.Modules);
        Assert.Equal(Web, loaded.Process);
        Assert.Equal([CoreLib, Generated, WebDll], loaded.Modules);
        Assert.Equal([TestPaths.FakePath("Web.Api.dll")], picker.Browser.ListToOpen());
        Assert.Equal(shutdown.Token, Assert.Single(ports.ListingTokens));
        Assert.Null(Follow(picker));
    }

    [Fact]
    public async Task ListToOpen_ModulesPickedOut_OpensOnlyThoseThatHaveAFile()
    {
        var ports = new FakePorts { Running = [Web], Modules = static (process, _) => ListedAsync(process, [CoreLib, Generated, WebDll]) };
        var picker = await OpenAsync(ports);
        await SelectAndListAsync(picker, Web);
        var modules = ProcessPicking.GetModules(picker.Browser.Modules);

        picker.Browser.ModuleSelection.SelectAll(modules);
        Assert.Equal([TestPaths.FakePath("System.Private.CoreLib.dll"), TestPaths.FakePath("Web.Api.dll")], picker.Browser.ListToOpen());

        picker.Browser.ModuleSelection.Select(Generated, SelectionChange.Replace, modules);
        Assert.False(picker.Browser.CanOpen);
        Assert.Empty(picker.Browser.ListToOpen());
    }

    [Fact]
    public async Task ListToOpen_AnotherProcessSelectedSinceTheModulesListed_OpensNothingOfTheProcessBefore()
    {
        var ports = new FakePorts { Running = [Web, Worker], Modules = static (process, _) => ListedAsync(process, [WebDll], TestPaths.FakePath("Web.Api.dll")) };
        var picker = await OpenAsync(ports);
        await SelectAndListAsync(picker, Web);

        picker.Browser.ProcessSelection.Select(picker.Browser.Filtered.ToList().IndexOf(Worker), picker.Browser.Filtered.Count);

        Assert.False(picker.Browser.CanOpen);
        Assert.Empty(picker.Browser.ListToOpen());
    }

    [Fact]
    public async Task FollowSelection_AnotherProcessSelected_DropsTheModulesShownAtOnceAndListsItsOwn()
    {
        var ports = new FakePorts { Running = [Web, Worker], Modules = static (process, _) => ListedAsync(process, [WebDll], TestPaths.FakePath("Web.Api.dll")) };
        var picker = await OpenAsync(ports);
        await SelectAndListAsync(picker, Web);

        var listing = Select(picker, Worker);

        Assert.Equal(Worker, listing.Process);
        Assert.Equal(Worker, Assert.IsType<ModuleScan.Loading>(picker.Browser.Modules).Process);
        Assert.Equal(0, picker.Browser.ModuleSelection.Count);
        Assert.False(picker.Browser.CanOpen);
        await picker.Browser.ListModulesAsync(listing, listing.Work.Token);
        Assert.Equal(Worker, Assert.IsType<ModuleScan.Loaded>(picker.Browser.Modules).Process);
    }

    [Fact]
    public async Task ListModulesAsync_ProcessUnreachable_StillOpensTheEntryAssemblyItsCommandLineNames()
    {
        var ports = new FakePorts
        {
            Running = [Worker],
            Modules = static (process, _) => AnswerAsync(new ModuleScan.Unreachable
            {
                Process = process,
                Detail = "gone",
                EntryAssembly = TestPaths.FakePath("Jobs.Worker.dll"),
            }),
        };
        var picker = await OpenAsync(ports);

        await SelectAndListAsync(picker, Worker);

        Assert.Equal("gone", Assert.IsType<ModuleScan.Unreachable>(picker.Browser.Modules).Detail);
        Assert.True(picker.Browser.CanOpen);
        Assert.Equal([TestPaths.FakePath("Jobs.Worker.dll")], picker.Browser.ListToOpen());
    }

    [Fact]
    public async Task ListModulesAsync_AnswerUnreadable_SaysSoAndHasNothingToOpen()
    {
        var ports = new FakePorts
        {
            Running = [Silent],
            Modules = static (process, _) => AnswerAsync(new ModuleScan.Unreadable
            {
                Process = process,
                Detail = "garbled",
                EntryAssembly = null,
            }),
        };
        var picker = await OpenAsync(ports);

        await SelectAndListAsync(picker, Silent);

        Assert.Equal("garbled", Assert.IsType<ModuleScan.Unreadable>(picker.Browser.Modules).Detail);
        Assert.False(picker.Browser.CanOpen);
    }

    [Fact]
    public async Task ListModulesAsync_ListingFailed_ReportsItAndSaysWhy()
    {
        var ports = new FakePorts
        {
            Running = [Web],
            Modules = static (_, _) => Task.FromResult<Outcome<ModuleScan>>(new Outcome<ModuleScan>.Failed(new InvalidDataException("broken"))),
        };
        var picker = await OpenAsync(ports);

        await SelectAndListAsync(picker, Web);

        Assert.Equal("broken", Assert.IsType<ModuleScan.Unreachable>(picker.Browser.Modules).Detail);
        Assert.IsType<AppProblem.Unexpected>(Assert.Single(ports.Reported));
    }

    [Fact]
    public async Task ListModulesAsync_AnotherProcessPicked_StopsTheFirstListingAndDropsItsResult()
    {
        var first = new TaskCompletionSource<Outcome<ModuleScan>>();
        var ports = new FakePorts
        {
            Running = [Web, Worker],
            Modules = (process, _) => process == Web ? first.Task : ListedAsync(process, [CoreLib]),
        };
        var picker = await OpenAsync(ports);
        var listingWeb = SelectAndListAsync(picker, Web);
        Assert.Equal(Web, Assert.IsType<ModuleScan.Loading>(picker.Browser.Modules).Process);

        await SelectAndListAsync(picker, Worker);
        first.SetResult(new Outcome<ModuleScan>.Ok(new ModuleScan.Loaded
        {
            Process = Web,
            Modules = [WebDll],
            EntryAssembly = null,
        }));
        await listingWeb;

        Assert.True(ports.ListingTokens[0].IsCancellationRequested);
        Assert.False(ports.ListingTokens[1].IsCancellationRequested);
        var loaded = Assert.IsType<ModuleScan.Loaded>(picker.Browser.Modules);
        Assert.Equal(Worker, loaded.Process);
        Assert.Equal([CoreLib], loaded.Modules);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task StopUnlessOpen_ClosedByTheCloseBoxWhileListing_StopsTheListingAndForgetsTheProcess()
    {
        var never = new TaskCompletionSource<Outcome<ModuleScan>>();
        var ports = new FakePorts { Running = [Web], Modules = (_, _) => never.Task };
        var picker = await OpenAsync(ports);
        var listing = SelectAndListAsync(picker, Web);

        Close(picker);
        never.SetResult(new Outcome<ModuleScan>.Ok(new ModuleScan.Loaded
        {
            Process = Web,
            Modules = [WebDll],
            EntryAssembly = null,
        }));
        await listing;

        Assert.True(Assert.Single(ports.ListingTokens).IsCancellationRequested);
        Assert.IsType<ModuleScan.None>(picker.Browser.Modules);
        Assert.Null(picker.Browser.FindSelectedProcess());
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public async Task ListModulesAsync_TokenFired_EndsCancelledWithoutAFailure()
    {
        var ports = new FakePorts { Running = [Web], Modules = static (_, stop) => Task.FromCanceled<Outcome<ModuleScan>>(stop) };
        var picker = await OpenAsync(ports);
        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => picker.Browser.ListModulesAsync(Select(picker, Web), shutdown.Token));

        Assert.True(Assert.Single(ports.ListingTokens).IsCancellationRequested);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task ScanAsync_Refreshed_KeepsTheProcessSelectedWhileItStillRuns()
    {
        var ports = new FakePorts { Running = [Web, Worker] };
        var picker = await OpenAsync(ports);
        await SelectAndListAsync(picker, Worker);

        ports.Running.RemoveAt(0);
        await ScanAsync(picker.Browser, picker.Browser.StartScan());

        Assert.Null(Follow(picker));
        Assert.Equal([Worker], picker.Browser.Filtered);
        Assert.Equal(Worker, picker.Browser.FindSelectedProcess());
        Assert.IsType<ModuleScan.Loaded>(picker.Browser.Modules);
    }

    [Fact]
    public async Task ScanAsync_WhileAListIsShown_KeepsItUntilTheNewListComes()
    {
        var refreshed = new TaskCompletionSource<Outcome<IReadOnlyList<RunningProcess>>>();
        var scans = 0;
        var ports = new FakePorts { Scan = _ => ++scans == 1 ? FoundAsync(Web) : refreshed.Task };
        var picker = await OpenAsync(ports);

        var refreshing = ScanAsync(picker.Browser, picker.Browser.StartScan());
        Assert.Equal([Web], Assert.IsType<ProcessScan.Scanned>(picker.Browser.Scan).Processes);
        refreshed.SetResult(new Outcome<IReadOnlyList<RunningProcess>>.Ok([Worker]));
        await refreshing;
        picker.Browser.TakeFilter(picker.Filter);

        Assert.Equal([Worker], picker.Browser.Filtered);
    }

    [Fact]
    public async Task ScanAsync_NewerScanStarted_DropsWhatTheOlderOneFinds()
    {
        var older = new TaskCompletionSource<Outcome<IReadOnlyList<RunningProcess>>>();
        var scans = 0;
        var ports = new FakePorts { Scan = _ => ++scans == 1 ? older.Task : FoundAsync(Worker) };
        var picker = new ProcessPicker { Browser = new ProcessBrowser(ports) };
        var olderTicket = picker.Open();
        var olderScan = ScanAsync(picker.Browser, olderTicket);
        Assert.IsType<ProcessScan.Scanning>(picker.Browser.Scan);

        await ScanAsync(picker.Browser, picker.Browser.StartScan());
        older.SetResult(new Outcome<IReadOnlyList<RunningProcess>>.Ok([Web]));
        await olderScan;
        picker.Browser.TakeFilter(picker.Filter);

        Assert.True(olderTicket.Token.IsCancellationRequested);
        Assert.Equal([Worker], picker.Browser.Filtered);
    }

    [Fact]
    public async Task ScanAsync_DialogClosedMeanwhile_DropsWhatItFinds()
    {
        var answer = new TaskCompletionSource<Outcome<IReadOnlyList<RunningProcess>>>();
        var ports = new FakePorts { Scan = _ => answer.Task };
        var picker = new ProcessPicker { Browser = new ProcessBrowser(ports) };
        var ticket = picker.Open();
        var scan = ScanAsync(picker.Browser, ticket);

        Close(picker);
        answer.SetResult(new Outcome<IReadOnlyList<RunningProcess>>.Ok([Web]));
        await scan;

        Assert.True(ticket.Token.IsCancellationRequested);
        Assert.IsType<ProcessScan.Scanning>(picker.Browser.Scan);
        Assert.Empty(picker.Browser.Filtered);
    }

    [Fact]
    public async Task ScanAsync_ListingFailed_SaysWhyAndReportsIt()
    {
        var ports = new FakePorts
        {
            Scan = static _ => Task.FromResult<Outcome<IReadOnlyList<RunningProcess>>>(new Outcome<IReadOnlyList<RunningProcess>>.Failed(new InvalidDataException("broken"))),
        };
        var picker = new ProcessPicker { Browser = new ProcessBrowser(ports) };

        await ScanAsync(picker.Browser, picker.Browser.StartScan());

        Assert.Equal("broken", Assert.IsType<ProcessScan.Failed>(picker.Browser.Scan).Error);
        Assert.IsType<AppProblem.Unexpected>(Assert.Single(ports.Reported));
    }

    [Fact]
    public void OpenFromProcess_ListingFailed_ShowsInTheDialogThatItFailedAndWhy()
    {
        var ports = new FakePorts
        {
            Scan = static _ => Task.FromResult<Outcome<IReadOnlyList<RunningProcess>>>(new Outcome<IReadOnlyList<RunningProcess>>.Failed(new InvalidDataException("broken"))),
        };
        var app = new AppDriver(processes: ports);
        app.Settle();

        app.Frames.ClickOn(Selector.WithText(Strings.En.MenuFile));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, Strings.En.FileOpenFromProcess));
        app.Settle();

        // The note wraps over lines; joined, they read as it does, and the error comes after it.
        Assert.Contains($"{Strings.En.ProcessPickerScanFailed} broken", string.Join(" ", app.Frames.TextRects().Select(static t => t.Text)));
        Assert.IsType<AppProblem.Unexpected>(Assert.Single(ports.Reported));
    }

    [Fact]
    public async Task Open_AfterAFailedScan_ShowsItScanningFromThatFrameNotTheFailure()
    {
        var scans = 0;
        var again = new TaskCompletionSource<Outcome<IReadOnlyList<RunningProcess>>>();
        var ports = new FakePorts
        {
            Scan = _ => ++scans == 1
                ? Task.FromResult<Outcome<IReadOnlyList<RunningProcess>>>(new Outcome<IReadOnlyList<RunningProcess>>.Failed(new InvalidDataException("broken")))
                : again.Task,
        };
        var picker = new ProcessPicker { Browser = new ProcessBrowser(ports) };
        await ScanAsync(picker.Browser, picker.Open());
        Close(picker);
        var failedBefore = picker.Browser.Scan;

        var scan = picker.Open();
        var shownAtOnce = picker.Browser.Scan;
        var asked = ScanAsync(picker.Browser, scan);
        again.SetResult(new Outcome<IReadOnlyList<RunningProcess>>.Ok([Web]));
        await asked;

        Assert.IsType<ProcessScan.Failed>(failedBefore);
        Assert.IsType<ProcessScan.Scanning>(shownAtOnce);
        Assert.IsType<ProcessScan.Scanned>(picker.Browser.Scan);
    }

    [Fact]
    public async Task ScanAsync_TokenFired_EndsCancelledWithoutAFailure()
    {
        var ports = new FakePorts { Scan = static ct => Task.FromCanceled<Outcome<IReadOnlyList<RunningProcess>>>(ct) };
        var picker = new ProcessPicker { Browser = new ProcessBrowser(ports) };
        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => picker.Browser.ScanAsync(picker.Browser.StartScan(), shutdown.Token));

        Assert.Equal(shutdown.Token, Assert.Single(ports.ScanTokens));
        Assert.IsNotType<ProcessScan.Failed>(picker.Browser.Scan);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task ListModulesAsync_AdapterTokenFired_EndsCancelledRatherThanFailed()
    {
        var adapter = new ProcessBrowserAdapter(static problem => throw new InvalidOperationException(problem.ToString()));
        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.ListModulesAsync(Web, shutdown.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.ListProcessesAsync(shutdown.Token));
    }

    [Fact]
    public async Task ScanAsync_ThisTestProcess_IsFoundWithTheAssembliesItLoaded()
    {
        var adapter = new ProcessBrowserAdapter(static problem => throw new InvalidOperationException(problem.ToString()));
        var picker = new ProcessPicker { Browser = new ProcessBrowser(adapter) };
        await ScanAsync(picker.Browser, picker.Open());
        picker.Browser.TakeFilter(picker.Filter);
        var self = Assert.Single(picker.Browser.Filtered, p => p.Id.Value == Environment.ProcessId);

        await SelectAndListAsync(picker, self);

        var modules = Assert.IsType<ModuleScan.Loaded>(picker.Browser.Modules).Modules;
        Assert.Contains(new RunningModule.OnDisk
        {
            Name = "ILSight.Core.dll",
            Path = TestPaths.PathOf(typeof(AssemblyList).Assembly.Location),
        }, modules);
    }
}
