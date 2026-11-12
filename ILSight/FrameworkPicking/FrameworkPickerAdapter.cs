using ILSight.Core;

namespace ILSight.FrameworkPicking;

/// <summary>Reaches the runtime installed on this machine, and the app's error reports, for
/// <see cref="FrameworkBrowser"/>. What the scan throws comes back as <see cref="Outcome{T}.Failed"/>, except the
/// cancellation of the token the call was given, which it lets through.</summary>
internal sealed class FrameworkPickerAdapter(Action<AppProblem> reportError, InstalledRuntime runtime) : FrameworkBrowser.IPorts
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
