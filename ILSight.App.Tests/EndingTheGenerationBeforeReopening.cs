using ILSight.AssemblyArea;
using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>The port the root hands the reload (<see cref="AdapterHooks.Reloader"/>): the real adapter, but the reopen
/// first runs <see cref="End"/> on the thread that owns the list, as a change that ends the list's generation and stops
/// no reload would, between the reload's clear and the reopen's check.</summary>
internal sealed class EndingTheGenerationBeforeReopening(AssemblyReloader.IPorts real) : AssemblyReloader.IPorts
{
    private int _reopens;

    /// <summary>Run as the first reopen begins, before it reads anything.</summary>
    public Action End { get; set; } = static () => { };

    /// <summary>Run as the first reopen has listed its files again, before the reload takes what it found; the reopen
    /// then ends as its token says (cancelled, when this stopped the reload).</summary>
    public Action AfterFirstReopen { get; set; } = static () => { };

    /// <summary>What each reopen after the first waits for before it reads, when set.</summary>
    public Task? LaterReopensWaitFor { get; set; }

    public void ReportError(AppProblem problem)
    {
        real.ReportError(problem);
    }

    public void ShowRefusal(Refusal refusal)
    {
        real.ShowRefusal(refusal);
    }

    public Task<PdbCheckOutcome> CheckPdbAsync(LoadedModule assembly, string pdbPath, CancellationToken ct)
    {
        return real.CheckPdbAsync(assembly, pdbPath, ct);
    }

    public Task<Outcome<ReopenedFile>> ReadAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct)
    {
        return real.ReadAgainAsync(item, places, ct);
    }

    public async Task<Outcome<ReloadedNodes>> ReopenFilesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded,
        IReadOnlyList<NodePath> places, GenerationWork work, CancellationToken ct)
    {
        _reopens++;
        if (_reopens == 1)
        {
            End();
            var reopened = await real.ReopenFilesAsync(paths, expanded, places, work, ct);
            AfterFirstReopen();
            ct.ThrowIfCancellationRequested();
            return reopened;
        }

        if (LaterReopensWaitFor is not null)
        {
            await LaterReopensWaitFor;
        }

        return await real.ReopenFilesAsync(paths, expanded, places, work, ct);
    }

    public void ReleaseModules(RemovedModules removed)
    {
        real.ReleaseModules(removed);
    }
}
