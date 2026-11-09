using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;

namespace ILSight;

/// <summary>The "open from framework" dialog: the runtime's assemblies not open yet, scanned once
/// in the background, filtered by name.</summary>
public sealed class FrameworkPicker
{
    /// <summary>Where the work it starts in the background is counted until it ends.</summary>
    public required BackgroundWork Work { get; init; }

    /// <summary>Tells the user the runtime could not be scanned.</summary>
    public required Action<AppProblem> ReportError { get; init; }

    /// <summary>Lists the assemblies to offer.</summary>
    public required Func<IReadOnlyList<RuntimeAssembly>> ListAssemblies { get; init; }

    public readonly List<RuntimeAssembly> Filtered = new();
    public readonly HashSet<AssemblyPath> Listed = new();
    public int ListedVersion = -1;
    public string? AppliedFilter;
    public bool FocusFilter;

    public bool IsOpen;
    public string Filter = "";
    public MultiSelection<RuntimeAssembly> Selection { get; } = new();

    public bool HasSelection => Selection.Count > 0;

    public RuntimeScan Scan { get; set; } = new RuntimeScan.NotStarted();
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
