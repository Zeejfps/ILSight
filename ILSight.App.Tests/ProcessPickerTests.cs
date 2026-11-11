using System.Collections.Concurrent;
using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Processes;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

public sealed class ProcessPickerTests
{
    private static readonly RunningProcess Web = Process(10, "Web", "Web.Api");
    private static readonly RunningProcess Worker = Process(22, "dotnet", "Jobs.Worker");
    private static readonly RunningProcess Silent = new(new ProcessId(31), "Stuck", null);

    private static readonly RunningModule WebDll = new RunningModule.OnDisk("Web.Api.dll", FakePath("Web.Api.dll"));
    private static readonly RunningModule CoreLib = new RunningModule.OnDisk("System.Private.CoreLib.dll", FakePath("System.Private.CoreLib.dll"));
    private static readonly RunningModule Generated = new RunningModule.InMemory("Generated");

    [Fact]
    public void Opening_lists_the_processes_and_the_filter_matches_name_id_or_entry_assembly()
    {
        var posted = PostedActions.OfThisTest();
        var picker = CreatePicker([Web, Worker, Silent], static (_, _) => Task.FromResult<ModuleListing>(new ModuleListing.Listed([])));

        OpenAndScan(picker, posted);

        Assert.Equal([Web, Worker, Silent], picker.Filtered);
        Assert.Equal([Worker], Filter(picker, "jobs"));
        Assert.Equal([Worker], Filter(picker, "22"));
        Assert.Equal([Silent], Filter(picker, "STUCK"));
        Assert.Empty(Filter(picker, "nothing"));
    }

    [Fact]
    public void Selecting_a_process_lists_its_modules_and_picks_out_its_entry_assembly()
    {
        var posted = PostedActions.OfThisTest();
        var picker = CreatePicker([Web], static (_, _) => Task.FromResult<ModuleListing>(new ModuleListing.Listed([CoreLib, Generated, WebDll])));
        OpenAndScan(picker, posted);

        Select(picker, Web, posted);

        var loaded = Assert.IsType<ModuleScan.Loaded>(picker.Modules);
        Assert.Equal(Web, loaded.Process);
        Assert.Equal([CoreLib, Generated, WebDll], loaded.Modules);
        Assert.Equal(FakePath("Web.Api.dll"), loaded.EntryAssembly);
        Assert.Equal([FakePath("Web.Api.dll")], ProcessPicking.ListToOpen(picker));
    }

    [Fact]
    public void Only_the_modules_picked_out_that_have_a_file_open()
    {
        var posted = PostedActions.OfThisTest();
        var picker = CreatePicker([Web], static (_, _) => Task.FromResult<ModuleListing>(new ModuleListing.Listed([CoreLib, Generated, WebDll])));
        OpenAndScan(picker, posted);
        Select(picker, Web, posted);

        picker.ModuleSelection.SelectAll(ProcessPicking.GetModules(picker.Modules));
        Assert.Equal([FakePath("System.Private.CoreLib.dll"), FakePath("Web.Api.dll")], ProcessPicking.ListToOpen(picker));

        picker.ModuleSelection.Select(Generated, ZGF.Gui.Immediate.Widgets.SelectionChange.Replace, ProcessPicking.GetModules(picker.Modules));
        Assert.False(ProcessPicking.CanOpen(picker));
        Assert.Empty(ProcessPicking.ListToOpen(picker));
    }

    [Fact]
    public void A_process_that_cannot_be_asked_still_opens_the_entry_assembly_its_command_line_names()
    {
        var posted = PostedActions.OfThisTest();
        var dll = AppStateDriver.FixturePath;
        var process = new RunningProcess(new ProcessId(40), "dotnet", new ProcessInfo
        {
            CommandLine = $"dotnet \"{dll}\"",
            Architecture = null,
            EntryAssemblyName = null,
            RuntimeVersion = null,
        });
        var picker = CreatePicker([process], static (_, _) => Task.FromResult<ModuleListing>(new ModuleListing.Unreachable("gone")));
        OpenAndScan(picker, posted);

        Select(picker, process, posted);

        Assert.Equal("gone", Assert.IsType<ModuleScan.Unreachable>(picker.Modules).Detail);
        Assert.True(ProcessPicking.CanOpen(picker));
        Assert.Equal([PathOf(dll)], ProcessPicking.ListToOpen(picker));
    }

    [Fact]
    public void An_answer_that_cannot_be_read_says_so_and_has_nothing_to_open()
    {
        var posted = PostedActions.OfThisTest();
        var picker = CreatePicker([Silent], static (_, _) => Task.FromResult<ModuleListing>(new ModuleListing.Unreadable("garbled")));
        OpenAndScan(picker, posted);

        Select(picker, Silent, posted);

        Assert.Equal("garbled", Assert.IsType<ModuleScan.Unreadable>(picker.Modules).Detail);
        Assert.False(ProcessPicking.CanOpen(picker));
    }

    [Fact]
    public void Picking_another_process_cancels_the_first_listing_and_drops_its_result()
    {
        var posted = PostedActions.OfThisTest();
        var first = new TaskCompletionSource<ModuleListing>();
        var tokens = new ConcurrentQueue<CancellationToken>();
        var picker = CreatePicker([Web, Worker], (id, token) =>
        {
            tokens.Enqueue(token);
            return id == Web.Id ? first.Task : Task.FromResult<ModuleListing>(new ModuleListing.Listed([CoreLib]));
        });
        OpenAndScan(picker, posted);

        SelectNow(picker, Web);
        Assert.Equal(Web, Assert.IsType<ModuleScan.Loading>(picker.Modules).Process);
        Poll.Until(() => tokens.Count == 1, posted.Run);
        Select(picker, Worker, posted);
        first.SetResult(new ModuleListing.Listed([WebDll]));
        RunFor(TimeSpan.FromMilliseconds(300), posted);

        Assert.True(tokens.First().IsCancellationRequested);
        var loaded = Assert.IsType<ModuleScan.Loaded>(picker.Modules);
        Assert.Equal(Worker, loaded.Process);
        Assert.Equal([CoreLib], loaded.Modules);
    }

    [Fact]
    public void Closing_cancels_the_listing_and_forgets_the_process()
    {
        var posted = PostedActions.OfThisTest();
        var never = new TaskCompletionSource<ModuleListing>();
        var tokens = new ConcurrentQueue<CancellationToken>();
        var picker = CreatePicker([Web], (_, token) =>
        {
            tokens.Enqueue(token);
            return never.Task;
        });
        OpenAndScan(picker, posted);
        SelectNow(picker, Web);
        Poll.Until(() => tokens.Count == 1, posted.Run);

        ProcessPicking.ClosePicker(picker);

        Assert.True(tokens.First().IsCancellationRequested);
        Assert.IsType<ModuleScan.None>(picker.Modules);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void Refreshing_keeps_the_process_selected_while_it_still_runs()
    {
        var posted = PostedActions.OfThisTest();
        var processes = new List<RunningProcess> { Web, Worker };
        var picker = CreatePicker(processes, static (_, _) => Task.FromResult<ModuleListing>(new ModuleListing.Listed([])));
        OpenAndScan(picker, posted);
        Select(picker, Worker, posted);

        processes.RemoveAt(0);
        ProcessPicking.Refresh(picker);
        Poll.Until(() => picker.AppliedFilter is null, posted.Run);
        ProcessPicking.UpdatePicker(picker);

        Assert.Equal(Worker, ProcessPicking.GetSelectedProcess(picker));
        Assert.IsType<ModuleScan.Loaded>(picker.Modules);
    }

    [Fact]
    public void A_listing_that_fails_unexpectedly_is_reported()
    {
        var posted = PostedActions.OfThisTest();
        var reported = new List<AppProblem>();
        var picker = new ProcessPicker
        {
            Work = new BackgroundWork(),
            ReportError = reported.Add,
            FindProcesses = static _ => throw new InvalidDataException("broken"),
            FindModules = static (_, _) => Task.FromResult<ModuleListing>(new ModuleListing.Listed([])),
        };

        ProcessPicking.OpenPicker(picker);
        Poll.Until(() => picker.Scan is not ProcessScan.Scanning, posted.Run);

        Assert.Equal("broken", Assert.IsType<ProcessScan.Failed>(picker.Scan).Error);
        Assert.IsType<AppProblem.Unexpected>(Assert.Single(reported));
    }

    [Fact]
    public void This_test_process_is_found_with_the_assemblies_it_loaded()
    {
        var posted = PostedActions.OfThisTest();
        var picker = new ProcessPicker
        {
            Work = new BackgroundWork(),
            ReportError = static problem => throw new InvalidOperationException(problem.ToString()),
            FindProcesses = RunningProcesses.FindProcessesAsync,
            FindModules = RunningProcesses.FindModulesAsync,
        };
        OpenAndScan(picker, posted);
        var self = Assert.Single(picker.Filtered, p => p.Id.Value == Environment.ProcessId);

        Select(picker, self, posted);

        var modules = Assert.IsType<ModuleScan.Loaded>(picker.Modules).Modules;
        Assert.Contains(new RunningModule.OnDisk("ILSight.Core.dll", PathOf(typeof(AssemblyList).Assembly.Location)), modules);
    }

    /// <summary>Runs what is posted for <paramref name="time"/>, for a result that must not arrive.</summary>
    private static void RunFor(TimeSpan time, PostedActions posted)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < time)
        {
            posted.Run();
            Thread.Sleep(5);
        }
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

    private static ProcessPicker CreatePicker(IReadOnlyList<RunningProcess> processes, Func<ProcessId, CancellationToken, Task<ModuleListing>> findModules)
    {
        return new ProcessPicker
        {
            Work = new BackgroundWork(),
            ReportError = static problem => throw new InvalidOperationException(problem.ToString()),
            FindProcesses = _ => Task.FromResult<IReadOnlyList<RunningProcess>>(processes.ToList()),
            FindModules = findModules,
        };
    }

    private static void OpenAndScan(ProcessPicker picker, PostedActions posted)
    {
        ProcessPicking.OpenPicker(picker);
        Poll.Until(() => picker.Scan is ProcessScan.Scanned or ProcessScan.Failed, posted.Run);
        ProcessPicking.UpdatePicker(picker);
    }

    private static List<RunningProcess> Filter(ProcessPicker picker, string filter)
    {
        picker.Filter = filter;
        ProcessPicking.UpdatePicker(picker);
        return picker.Filtered.ToList();
    }

    /// <summary>Selects <paramref name="process"/> and starts listing its modules, without waiting for them.</summary>
    private static void SelectNow(ProcessPicker picker, RunningProcess process)
    {
        picker.ProcessSelection.Select(picker.Filtered.IndexOf(process), picker.Filtered.Count);
        ProcessPicking.UpdatePicker(picker);
    }

    /// <summary>Selects <paramref name="process"/> and waits for its modules.</summary>
    private static void Select(ProcessPicker picker, RunningProcess process, PostedActions posted)
    {
        SelectNow(picker, process);
        Poll.Until(() => picker.Modules is not ModuleScan.Loading, posted.Run);
    }
}
