using ILSight.Core;
using ILSight.Core.Processes;
using ZGF.Gui.Immediate.Widgets;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

/// <summary>The "open from running process" dialog's workflows over a fake port: what each does with what the port
/// answers, and the tokens it asks with; and its adapter over this test's own process.</summary>
public sealed class ProcessPickerTests
{
    private static readonly RunningProcess Web = Process(10, "Web", "Web.Api");
    private static readonly RunningProcess Worker = Process(22, "dotnet", "Jobs.Worker");
    private static readonly RunningProcess Silent = new(new ProcessId(31), "Stuck", null);

    private static readonly RunningModule WebDll = new RunningModule.OnDisk("Web.Api.dll", FakePath("Web.Api.dll"));
    private static readonly RunningModule CoreLib = new RunningModule.OnDisk("System.Private.CoreLib.dll", FakePath("System.Private.CoreLib.dll"));
    private static readonly RunningModule Generated = new RunningModule.InMemory("Generated");

    /// <summary>Answers each port member with what the test set; keeps the tokens it was asked with, and the
    /// problems reported.</summary>
    private sealed class FakePorts : ProcessBrowser.IPorts
    {
        /// <summary>The processes the next scans find; a test changes it as processes come and go.</summary>
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
        return AnswerAsync(new ModuleScan.Loaded(process, modules, entry));
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
        return new RunningProcess(new ProcessId(id), name, new ProcessInfo
        {
            CommandLine = null,
            Architecture = "arm64",
            EntryAssemblyName = entry,
            RuntimeVersion = "10.0.0",
        });
    }

    /// <summary>Runs <paramref name="scan"/> as the edge does, on the token of the work it was started as, which the
    /// edge links to the app's shutdown.</summary>
    private static Task ScanAsync(ProcessBrowser browser, WorkTicket scan)
    {
        return browser.ScanAsync(scan, scan.Token);
    }

    /// <summary>A picker over <paramref name="ports"/>, opened, its processes found and filtered as the frame does.</summary>
    private static async Task<ProcessPicker> OpenAsync(FakePorts ports)
    {
        var picker = new ProcessPicker { Browser = new ProcessBrowser(ports) };
        await ScanAsync(picker.Browser, picker.Open());
        Assert.Null(picker.Update());
        return picker;
    }

    /// <summary>Closes the dialog as its close box does, then tells what it found, as each frame does.</summary>
    private static void Close(ProcessPicker picker)
    {
        picker.IsOpen = false;
        picker.Browser.StopUnlessOpen(picker.IsOpen);
    }

    private static List<RunningProcess> Filter(ProcessPicker picker, string filter)
    {
        picker.Filter = filter;
        picker.Update();
        return picker.Browser.Filtered.ToList();
    }

    /// <summary>Selects <paramref name="process"/> as the list does; the process whose modules the frame then asks
    /// for, which must be it.</summary>
    private static RunningProcess Select(ProcessPicker picker, RunningProcess process)
    {
        var filtered = picker.Browser.Filtered;
        picker.Browser.ProcessSelection.Select(filtered.ToList().IndexOf(process), filtered.Count);
        var asked = picker.Update();
        Assert.NotNull(asked);
        Assert.Equal(process, asked);
        return asked;
    }

    /// <summary>Selects <paramref name="process"/> and lists its modules, as the frame does, on the token of the work
    /// the listing was started as.</summary>
    private static Task SelectAndListAsync(ProcessPicker picker, RunningProcess process)
    {
        var listing = picker.Browser.StartListing(Select(picker, process));
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
        var ports = new FakePorts { Running = [Web], Modules = static (process, _) => ListedAsync(process, [CoreLib, Generated, WebDll], FakePath("Web.Api.dll")) };
        var picker = await OpenAsync(ports);
        using var shutdown = new CancellationTokenSource();

        await picker.Browser.ListModulesAsync(picker.Browser.StartListing(Select(picker, Web)), shutdown.Token);

        var loaded = Assert.IsType<ModuleScan.Loaded>(picker.Browser.Modules);
        Assert.Equal(Web, loaded.Process);
        Assert.Equal([CoreLib, Generated, WebDll], loaded.Modules);
        Assert.Equal([FakePath("Web.Api.dll")], picker.Browser.ListToOpen());
        Assert.Equal(shutdown.Token, Assert.Single(ports.ListingTokens));
        Assert.Null(picker.Update());
    }

    [Fact]
    public async Task ListToOpen_ModulesPickedOut_OpensOnlyThoseThatHaveAFile()
    {
        var ports = new FakePorts { Running = [Web], Modules = static (process, _) => ListedAsync(process, [CoreLib, Generated, WebDll]) };
        var picker = await OpenAsync(ports);
        await SelectAndListAsync(picker, Web);
        var modules = ProcessPicking.GetModules(picker.Browser.Modules);

        picker.Browser.ModuleSelection.SelectAll(modules);
        Assert.Equal([FakePath("System.Private.CoreLib.dll"), FakePath("Web.Api.dll")], picker.Browser.ListToOpen());

        picker.Browser.ModuleSelection.Select(Generated, SelectionChange.Replace, modules);
        Assert.False(picker.Browser.CanOpen);
        Assert.Empty(picker.Browser.ListToOpen());
    }

    [Fact]
    public async Task ListModulesAsync_ProcessUnreachable_StillOpensTheEntryAssemblyItsCommandLineNames()
    {
        var ports = new FakePorts
        {
            Running = [Worker],
            Modules = static (process, _) => AnswerAsync(new ModuleScan.Unreachable(process, "gone", FakePath("Jobs.Worker.dll"))),
        };
        var picker = await OpenAsync(ports);

        await SelectAndListAsync(picker, Worker);

        Assert.Equal("gone", Assert.IsType<ModuleScan.Unreachable>(picker.Browser.Modules).Detail);
        Assert.True(picker.Browser.CanOpen);
        Assert.Equal([FakePath("Jobs.Worker.dll")], picker.Browser.ListToOpen());
    }

    [Fact]
    public async Task ListModulesAsync_AnswerUnreadable_SaysSoAndHasNothingToOpen()
    {
        var ports = new FakePorts { Running = [Silent], Modules = static (process, _) => AnswerAsync(new ModuleScan.Unreadable(process, "garbled", null)) };
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
        first.SetResult(new Outcome<ModuleScan>.Ok(new ModuleScan.Loaded(Web, [WebDll], null)));
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
        never.SetResult(new Outcome<ModuleScan>.Ok(new ModuleScan.Loaded(Web, [WebDll], null)));
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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => picker.Browser.ListModulesAsync(picker.Browser.StartListing(Select(picker, Web)), shutdown.Token));

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

        Assert.Null(picker.Update());
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
        picker.Update();

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
        picker.Update();

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
        picker.Update();
        var self = Assert.Single(picker.Browser.Filtered, p => p.Id.Value == Environment.ProcessId);

        await SelectAndListAsync(picker, self);

        var modules = Assert.IsType<ModuleScan.Loaded>(picker.Browser.Modules).Modules;
        Assert.Contains(new RunningModule.OnDisk("ILSight.Core.dll", PathOf(typeof(AssemblyList).Assembly.Location)), modules);
    }
}
