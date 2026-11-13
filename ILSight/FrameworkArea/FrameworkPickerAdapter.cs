using ILSight.Core;

namespace ILSight.FrameworkArea;

/// <summary>Reaches the runtime installed on this machine, and the app's error reports, for
/// <see cref="FrameworkBrowser"/>. What the scan throws comes back as <see cref="Outcome{T}.Failed"/>, except the
/// cancellation of the token the call was given, which it lets through.</summary>
internal sealed class FrameworkPickerAdapter(Action<AppProblem> reportError, InstalledRuntime runtime) : FrameworkBrowser.IPorts
{
    /// <summary>The runtime's assemblies, scanned once, the first time they are asked for, and again the next time
    /// when the scan failed.</summary>
    private readonly Lazy<IReadOnlyList<RuntimeAssembly>> _assemblies = new(() => RuntimeFolders.ScanAssemblies(runtime.Directories), LazyThreadSafetyMode.PublicationOnly);

    public void ReportError(AppProblem problem)
    {
        reportError(problem);
    }

    /// <summary>Scans on the thread pool and comes back on the UI thread, with a file it could not read (<see cref="FileReadFailures"/>) as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public Task<Outcome<IReadOnlyList<RuntimeAssembly>>> ListAssembliesAsync(CancellationToken ct)
    {
        return FileReadFailures.CatchAsync(Background.Wait(Task.Run(() => _assemblies.Value, ct)), ct);
    }
}
