using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;

namespace ILSight;

/// <summary>The "open from framework" dialog: the runtime's assemblies not open yet, scanned once,
/// filtered by name.</summary>
public sealed class FrameworkPicker(FrameworkPicker.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the runtime could not be scanned.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Lists the assemblies to offer, without holding up the screen while it scans; a runtime that
        /// could not be scanned comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<IReadOnlyList<RuntimeAssembly>>> ListAssembliesAsync(CancellationToken ct);
    }

    /// <summary>The assemblies offered, in the order shown.</summary>
    public readonly List<RuntimeAssembly> Filtered = new();
    private readonly HashSet<AssemblyPath> _listed = new();
    private int _listedVersion = -1;
    private string? _appliedFilter;
    private bool _focusFilter;

    public bool IsOpen;
    public string Filter = "";
    public MultiSelection<RuntimeAssembly> Selection { get; } = new();

    public bool HasSelection => Selection.Count > 0;

    public RuntimeScan Scan { get; private set; } = new RuntimeScan.NotStarted();

    /// <summary>The picked-out assemblies, in the order they are shown.</summary>
    public List<RuntimeAssembly> ListSelected()
    {
        return Selection.InOrder(Filtered);
    }

    /// <summary>Opens the dialog at once; scans the runtime the first time it opens, and again after a scan
    /// failed, ending when the scan does.</summary>
    public async Task OpenAsync(CancellationToken ct)
    {
        IsOpen = true;
        _focusFilter = true;
        _appliedFilter = null;
        if (Scan is RuntimeScan.Scanned or RuntimeScan.Scanning)
        {
            return;
        }

        Scan = new RuntimeScan.Scanning();
        var scanned = await ports.ListAssembliesAsync(ct);
        _appliedFilter = null;
        Scan = scanned.Match<RuntimeScan>(static ok => new RuntimeScan.Scanned(ok.Value), ReportFailedScan);
    }

    private RuntimeScan.Failed ReportFailedScan(Outcome<IReadOnlyList<RuntimeAssembly>>.Failed failed)
    {
        ports.ReportError(new AppProblem.RuntimeNotScanned(failed.Error));
        return new RuntimeScan.Failed(failed.Error.ToString());
    }

    /// <summary>True once after <see cref="OpenAsync"/>: the filter box should take focus.</summary>
    public bool TryTakeFilterFocus()
    {
        var focus = _focusFilter;
        _focusFilter = false;
        return focus;
    }

    /// <summary>While open, re-filters when the assembly list or the filter text changed since the last frame.</summary>
    public void Update(AssemblyList assemblies)
    {
        if (!IsOpen)
        {
            return;
        }

        RefreshListed(assemblies);
        ApplyFilter();
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

    private void ApplyFilter()
    {
        if (Scan is not RuntimeScan.Scanned scanned)
        {
            return;
        }

        if (Filter == _appliedFilter)
        {
            return;
        }

        _appliedFilter = Filter;
        Filtered.Clear();
        var filter = Filter.Trim();
        foreach (var assembly in scanned.Assemblies)
        {
            if (Offers(assembly, filter))
            {
                Filtered.Add(assembly);
            }
        }
        Selection.Reconcile(Filtered);
        if (!HasSelection)
        {
            Selection.MoveTo(0, extend: false, Filtered);
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
/// <see cref="FrameworkPicker"/>.</summary>
public sealed class FrameworkPickerAdapter(Action<AppProblem> reportError, InstalledRuntime runtime) : FrameworkPicker.IPorts
{
    public void ReportError(AppProblem problem)
    {
        reportError(problem);
    }

    /// <summary>Scans on the thread pool and comes back on the UI thread, with whatever the scan threw as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<IReadOnlyList<RuntimeAssembly>>> ListAssembliesAsync(CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => RuntimeAssemblies.ListAssemblies(runtime), ct));
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
