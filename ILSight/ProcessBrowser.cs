using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Processes;

namespace ILSight;

/// <summary>
/// What the "open from running process" dialog found, and the rules it keeps: the running .NET processes, looked
/// for each time it opens and on Refresh, those shown kept until the newest scan's list comes; those the filter
/// matches; the process selected among them, whose modules are listed once it is another than the one shown, the
/// listing before it stopped and its result dropped; and what Open opens. Its workflows return once their work is
/// done, and reach the processes and the user's error reports through <see cref="IPorts"/>.
/// </summary>
public sealed class ProcessBrowser(ProcessBrowser.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user that looking for the processes or their modules failed unexpectedly.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The running .NET processes, listed without holding up the screen; a listing that failed comes
        /// back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<IReadOnlyList<RunningProcess>>> ListProcessesAsync(CancellationToken ct);

        /// <summary>The modules <paramref name="process"/> has loaded and its entry assembly, listed without holding
        /// up the screen; a listing that failed comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<ModuleScan>> ListModulesAsync(RunningProcess process, CancellationToken ct);
    }

    /// <summary>A listing <see cref="StartListing"/> started, for <see cref="ListModulesAsync"/> to run: the
    /// process, and the work it runs as, which picking another process or closing the dialog cancels.</summary>
    public sealed class ListingTicket
    {
        public required RunningProcess Process { get; init; }

        public required WorkTicket Work { get; init; }
    }

    private readonly List<RunningProcess> _filtered = [];
    private readonly LatestWork _scans = new();
    private readonly LatestWork _listings = new();
    private string? _appliedFilter;
    private bool _focusFilter;
    private bool _stopped = true;

    /// <summary>How far looking for the running processes has got.</summary>
    public ProcessScan Scan { get; private set; } = new ProcessScan.NotStarted();

    /// <summary>The processes the filter shows, in the order shown.</summary>
    public IReadOnlyList<RunningProcess> Filtered => _filtered;

    /// <summary>The process whose modules are shown, as a row of <see cref="Filtered"/>.</summary>
    public ListSelection ProcessSelection { get; } = new();

    /// <summary>How far listing the selected process's modules has got.</summary>
    public ModuleScan Modules { get; private set; } = new ModuleScan.None();

    public MultiSelection<RunningModule> ModuleSelection { get; } = new();

    /// <summary>Whether Open has anything to open, worked out without building the list.</summary>
    public bool CanOpen
    {
        get
        {
            if (ModuleSelection.Count == 0)
            {
                return ProcessPicking.FindEntryAssembly(Modules) != null;
            }

            foreach (var module in ProcessPicking.GetModules(Modules))
            {
                if (module is RunningModule.OnDisk && ModuleSelection.Contains(module))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Has the filter take focus once, the next time <see cref="TryTakeFilterFocus"/> is asked.</summary>
    public void FocusFilter()
    {
        _focusFilter = true;
    }

    /// <summary>True once after <see cref="FocusFilter"/>: the filter box should take focus.</summary>
    public bool TryTakeFilterFocus()
    {
        var focus = _focusFilter;
        _focusFilter = false;
        return focus;
    }

    /// <summary>Starts looking for the processes again, in place of the scan running, which is cancelled and its
    /// list dropped; those shown are kept until the new list comes, and with none shown the dialog draws it
    /// scanning from now on. Returns the work <see cref="ScanAsync"/> scans as, which another scan or closing the
    /// dialog cancels.</summary>
    public WorkTicket StartScan()
    {
        _stopped = false;
        if (Scan is not ProcessScan.Scanned)
        {
            Scan = new ProcessScan.Scanning();
        }

        return _scans.Start();
    }

    /// <summary>Looks for the processes, as <paramref name="scan"/>, which <see cref="StartScan"/> started:
    /// the new list takes the place of those shown unless another scan started or the dialog closed meanwhile.
    /// A listing that failed says so and is reported.</summary>
    public async Task ScanAsync(WorkTicket scan, CancellationToken ct)
    {
        var found = await ports.ListProcessesAsync(ct);
        if (!_scans.TryFinish(scan))
        {
            return;
        }

        _appliedFilter = null;
        Scan = found.Match<ProcessScan>(static ok => new ProcessScan.Scanned(ok.Value), ReportScanFailure);
    }

    private ProcessScan.Failed ReportScanFailure(Outcome<IReadOnlyList<RunningProcess>>.Failed failed)
    {
        // Listing reports a process it can't reach as a row, so a failure here is a bug.
        ports.ReportError(new AppProblem.Unexpected(failed.Error));
        return new ProcessScan.Failed(failed.Error.Message);
    }

    /// <summary>Told each frame whether the dialog is open, however it was closed (the close box, Escape, Cancel or
    /// Open): once it is not, stops the scan and the module listing running, dropping what they find, and forgets
    /// the process picked.</summary>
    public void StopUnlessOpen(bool isOpen)
    {
        if (isOpen)
        {
            _stopped = false;
            return;
        }

        if (_stopped)
        {
            return;
        }

        _stopped = true;
        Stop();
    }

    private void Stop()
    {
        _scans.Cancel();
        StopListing();
        Modules = new ModuleScan.None();
        ProcessSelection.Clear();
    }

    /// <summary>Re-filters when the processes or <paramref name="filter"/> changed, and follows the process
    /// selected: returns it, for its modules to be listed now, once it is another than the one whose modules are
    /// shown; else null. A selection cleared stops the listing and shows no modules.</summary>
    public RunningProcess? Update(string filter)
    {
        ApplyFilter(filter);
        return FollowSelection();
    }

    private void ApplyFilter(string filter)
    {
        if (Scan is not ProcessScan.Scanned scanned)
        {
            return;
        }

        if (filter == _appliedFilter)
        {
            return;
        }

        _appliedFilter = filter;
        var selected = FindSelectedProcess();
        _filtered.Clear();
        var trimmed = filter.Trim();
        foreach (var process in scanned.Processes)
        {
            if (ProcessPicking.IsMatch(process, trimmed))
            {
                _filtered.Add(process);
            }
        }
        var index = selected == null ? -1 : _filtered.FindIndex(p => p.Id == selected.Id);
        ProcessSelection.Select(index, _filtered.Count, reveal: false);
    }

    /// <summary>The process selected among those shown, or null.</summary>
    public RunningProcess? FindSelectedProcess()
    {
        var index = ProcessSelection.Index;
        if (index < 0 || index >= _filtered.Count)
        {
            return null;
        }

        return _filtered[index];
    }

    private RunningProcess? FollowSelection()
    {
        var selected = FindSelectedProcess();
        var shown = ProcessPicking.FindProcess(Modules);
        if (selected?.Id == shown?.Id)
        {
            return null;
        }

        if (selected == null)
        {
            StopListing();
            Modules = new ModuleScan.None();
        }
        return selected;
    }

    /// <summary>Starts listing the modules of <paramref name="process"/> in place of the listing running, which is
    /// stopped and its result dropped: the dialog draws them loading from now on. Returns what
    /// <see cref="ListModulesAsync"/> needs to list them, whose work picking another process or closing the dialog
    /// cancels.</summary>
    public ListingTicket StartListing(RunningProcess process)
    {
        _stopped = false;
        var work = _listings.Start();
        ModuleSelection.Clear();
        Modules = new ModuleScan.Loading(process);
        return new ListingTicket { Process = process, Work = work };
    }

    /// <summary>Lists the modules of the process <paramref name="listing"/> is of, and shows them unless another
    /// process was picked or the dialog closed meanwhile, the entry assembly's picked out. A listing that failed
    /// unexpectedly is reported.</summary>
    public async Task ListModulesAsync(ListingTicket listing, CancellationToken ct)
    {
        var process = listing.Process;
        var listed = await ports.ListModulesAsync(process, ct);
        if (!_listings.TryFinish(listing.Work))
        {
            return;
        }

        Modules = listed.Match<ModuleScan>(static ok => ok.Value, failed => ReportListingFailure(process, failed.Error));
        SelectEntryModule();
    }

    private ModuleScan.Unreachable ReportListingFailure(RunningProcess process, Exception error)
    {
        // A process that can't be asked is a result, so a failure here is a bug.
        ports.ReportError(new AppProblem.Unexpected(error));
        return new ModuleScan.Unreachable(process, error.Message, null);
    }

    private void StopListing()
    {
        _listings.Cancel();
        ModuleSelection.Clear();
    }

    /// <summary>Picks out the entry assembly's module, so that Open opens it unless another is picked.</summary>
    private void SelectEntryModule()
    {
        if (Modules is not ModuleScan.Loaded loaded || loaded.EntryAssembly == null)
        {
            return;
        }

        foreach (var module in loaded.Modules)
        {
            if (module is RunningModule.OnDisk onDisk && onDisk.Path.Equals(loaded.EntryAssembly))
            {
                ModuleSelection.Select(module, SelectionChange.Replace, loaded.Modules);
                return;
            }
        }
    }

    /// <summary>What Open opens: the modules picked out that have a file, in the order shown; with none picked
    /// out, the entry assembly, which may be no module, as a single-file app's executable isn't.</summary>
    public List<AssemblyPath> ListToOpen()
    {
        if (ModuleSelection.Count == 0)
        {
            var entry = ProcessPicking.FindEntryAssembly(Modules);
            return entry == null ? [] : [entry];
        }
        var paths = new List<AssemblyPath>();
        foreach (var module in ModuleSelection.InOrder(ProcessPicking.GetModules(Modules)))
        {
            if (module is RunningModule.OnDisk onDisk)
            {
                paths.Add(onDisk.Path);
            }
        }
        return paths;
    }
}
