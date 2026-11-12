using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;

namespace ILSight;

/// <summary>The "open from framework" dialog as its widgets bind it: whether it is open and the filter typed;
/// and <see cref="Browser"/>, the runtime's assemblies it offers, which keeps its own rules.</summary>
public sealed class FrameworkPicker
{
    public bool IsOpen;
    public string Filter = "";

    /// <summary>The runtime's assemblies, scanned once, those the filter shows and those picked out.</summary>
    public required FrameworkBrowser Browser { get; init; }

    /// <summary>Opens the dialog, its filter taking focus, and starts scanning the runtime unless it was scanned or
    /// is being scanned: the first time the dialog opens, and again after a scan failed. The dialog draws it
    /// scanning from this frame on. Returns the work <see cref="FrameworkBrowser.ScanAsync"/> scans as, or null when
    /// there is nothing to scan.</summary>
    public WorkTicket? Open()
    {
        IsOpen = true;
        return Browser.Open();
    }

    /// <summary>While open, takes in the filter typed and the assemblies open; see
    /// <see cref="FrameworkBrowser.Update"/>.</summary>
    public void Update(AssemblyList assemblies)
    {
        if (!IsOpen)
        {
            return;
        }

        Browser.Update(assemblies, Filter);
    }
}

/// <summary>What the "open from framework" dialog offers, and the rules it keeps: the runtime's assemblies not open
/// yet, scanned once, filtered by name, those picked out among them. Its workflow returns once its work is done,
/// and reaches the runtime and the user's error reports through <see cref="IPorts"/>.</summary>
public sealed class FrameworkBrowser(FrameworkBrowser.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the runtime could not be scanned.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Lists the assemblies to offer, without holding up the screen while it scans; a runtime that
        /// could not be scanned comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<IReadOnlyList<RuntimeAssembly>>> ListAssembliesAsync(CancellationToken ct);
    }

    private readonly List<RuntimeAssembly> _filtered = new();
    private readonly HashSet<AssemblyPath> _listed = new();
    private readonly LatestWork _scans = new();
    private int _listedVersion = -1;
    private string? _appliedFilter;
    private bool _focusFilter;

    /// <summary>The assemblies offered, in the order shown.</summary>
    public IReadOnlyList<RuntimeAssembly> Filtered => _filtered;

    public MultiSelection<RuntimeAssembly> Selection { get; } = new();

    public bool HasSelection => Selection.Count > 0;

    public RuntimeScan Scan { get; private set; } = new RuntimeScan.NotStarted();

    /// <summary>The picked-out assemblies, in the order they are shown.</summary>
    public List<RuntimeAssembly> ListSelected()
    {
        return Selection.InOrder(_filtered);
    }

    /// <summary>As the dialog opens: has the filter take focus and filters again, and starts scanning the runtime
    /// unless it was scanned or is being scanned, drawn scanning from now on. Returns the work
    /// <see cref="ScanAsync"/> scans as, or null when there is nothing to scan.</summary>
    public WorkTicket? Open()
    {
        _focusFilter = true;
        _appliedFilter = null;
        if (Scan is RuntimeScan.Scanned or RuntimeScan.Scanning)
        {
            return null;
        }

        Scan = new RuntimeScan.Scanning();
        return _scans.Start();
    }

    /// <summary>Scans the runtime for the assemblies to offer, as <paramref name="scan"/>, which
    /// <see cref="Open"/> started. A runtime that could not be scanned says so and is reported.</summary>
    public async Task ScanAsync(WorkTicket scan, CancellationToken ct)
    {
        var scanned = await ports.ListAssembliesAsync(ct);
        if (!_scans.TryFinish(scan))
        {
            return;
        }

        _appliedFilter = null;
        Scan = scanned.Match<RuntimeScan>(static ok => new RuntimeScan.Scanned(ok.Value), ReportFailedScan);
    }

    private RuntimeScan.Failed ReportFailedScan(Outcome<IReadOnlyList<RuntimeAssembly>>.Failed failed)
    {
        ports.ReportError(new AppProblem.RuntimeNotScanned(failed.Error));
        return new RuntimeScan.Failed(failed.Error.ToString());
    }

    /// <summary>True once after <see cref="Open"/>: the filter box should take focus.</summary>
    public bool TryTakeFilterFocus()
    {
        var focus = _focusFilter;
        _focusFilter = false;
        return focus;
    }

    /// <summary>Re-filters when the assembly list or <paramref name="filter"/> changed since the last frame.</summary>
    public void Update(AssemblyList assemblies, string filter)
    {
        RefreshListed(assemblies);
        ApplyFilter(filter);
    }

    private void RefreshListed(AssemblyList assemblies)
    {
        if (assemblies.Version == _listedVersion)
        {
            return;
        }

        _listedVersion = assemblies.Version;
        _listed.Clear();
        _listed.UnionWith(AssemblyListing.ListFilePaths(assemblies));
        _appliedFilter = null;
    }

    private void ApplyFilter(string filter)
    {
        if (Scan is not RuntimeScan.Scanned scanned)
        {
            return;
        }

        if (filter == _appliedFilter)
        {
            return;
        }

        _appliedFilter = filter;
        _filtered.Clear();
        var trimmed = filter.Trim();
        foreach (var assembly in scanned.Assemblies)
        {
            if (Offers(assembly, trimmed))
            {
                _filtered.Add(assembly);
            }
        }
        Selection.Reconcile(_filtered);
        if (!HasSelection)
        {
            Selection.MoveTo(0, extend: false, _filtered);
        }
    }

    /// <summary>Whether <paramref name="assembly"/> is not open yet and its name matches
    /// <paramref name="filter"/>.</summary>
    private bool Offers(RuntimeAssembly assembly, string filter)
    {
        if (_listed.Contains(assembly.FilePath))
        {
            return false;
        }

        if (filter.Length == 0)
        {
            return true;
        }

        return assembly.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Reaches the runtime installed on this machine, and the app's error reports, for
/// <see cref="FrameworkBrowser"/>. What the scan throws comes back as <see cref="Outcome{T}.Failed"/>, except the
/// cancellation of the token the call was given, which it lets through.</summary>
public sealed class FrameworkPickerAdapter(Action<AppProblem> reportError, InstalledRuntime runtime) : FrameworkBrowser.IPorts
{
    public void ReportError(AppProblem problem)
    {
        reportError(problem);
    }

    /// <summary>Scans on the thread pool and comes back on the UI thread, with whatever the scan threw as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<IReadOnlyList<RuntimeAssembly>>> ListAssembliesAsync(CancellationToken ct)
    {
        var scanned = await Background.Wait(Task.Run(() => RuntimeAssemblies.ListAssemblies(runtime), ct));
        ct.ThrowIfCancellationRequested();
        return scanned;
    }
}

/// <summary>How far scanning the runtime for its assemblies has got.</summary>
public abstract record RuntimeScan
{
    private RuntimeScan()
    {
    }

    public sealed record NotStarted : RuntimeScan;

    public sealed record Scanning : RuntimeScan;

    public sealed record Scanned(IReadOnlyList<RuntimeAssembly> Assemblies) : RuntimeScan;

    public sealed record Failed(string Error) : RuntimeScan;
}
