using ILSight.AssemblyArea;
using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>The port the root hands the reload (<see cref="AdapterHooks.Reloading"/>): the real adapter, but as the reopen
/// begins, <see cref="End"/> runs on a thread of its own and is held inside the cancel of the generation's ending, after
/// the list's lock is released: a callback registered on that ending after the edge linked it runs first (callbacks run
/// newest first) and waits there until <see cref="Release"/>. The reopen's check runs meanwhile, the ending marked
/// cancelled and the edge's linked token not reached yet.</summary>
internal sealed class HoldingTheGenerationsEndAtTheReopen(AssemblyReloading.IPorts real) : AssemblyReloading.IPorts
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private readonly ManualResetEventSlim _cancelling = new();
    private readonly ManualResetEventSlim _release = new();
    private Thread? _ending;
    private CancellationTokenRegistration _gate;

    public Action End { get; set; } = static () => { };

    /// <summary>Whether the edge's token had been reached when the check ran: null before it ran.</summary>
    public bool? LinkedReachedAtCheck { get; private set; }

    /// <summary>Whether the generation's ending was marked cancelled when the check ran: null before it ran.</summary>
    public bool? EndingMarkedAtCheck { get; private set; }

    public void ReportError(AppProblem problem)
    {
        real.ReportError(problem);
    }

    public void ShowRefusal(Refusal refusal)
    {
        real.ShowRefusal(refusal);
    }

    public Task<PdbChecking> CheckPdbAsync(LoadedModule assembly, string pdbPath, CancellationToken ct)
    {
        return real.CheckPdbAsync(assembly, pdbPath, ct);
    }

    public Task<Outcome<ReopenedFile>> ReadAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct)
    {
        return real.ReadAgainAsync(item, places, ct);
    }

    public Task<Outcome<ReloadedNodes>> ReopenFilesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded, IReadOnlyList<NodePath> places,
        GenerationWork work, CancellationToken ct)
    {
        _gate = work.Ending.Register(Hold);
        _ending = new Thread(() => End());
        _ending.Start();
        // Not the reopen's token: it is reached only once the held cancel goes on.
        if (!_cancelling.Wait(Bound, CancellationToken.None))
        {
            throw new TimeoutException($"The generation's end never began cancelling within {Bound.TotalSeconds} s.");
        }

        LinkedReachedAtCheck = ct.IsCancellationRequested;
        EndingMarkedAtCheck = work.Ending.IsCancellationRequested;
        return real.ReopenFilesAsync(paths, expanded, places, work, ct);
    }

    public void ReleaseModules(RemovedModules removed)
    {
        real.ReleaseModules(removed);
    }

    /// <summary>Lets the held end finish and waits for it.</summary>
    public void Release()
    {
        _release.Set();
        var ending = _ending ?? throw new InvalidOperationException("The reopen never began.");
        if (!ending.Join(Bound))
        {
            throw new TimeoutException($"The generation's end never finished within {Bound.TotalSeconds} s.");
        }

        _gate.Dispose();
    }

    private void Hold()
    {
        _cancelling.Set();
        _release.Wait(Bound);
    }
}
