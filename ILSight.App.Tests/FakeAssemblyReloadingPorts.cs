using ILSight.Core;
using ILSight.AssemblyArea;

namespace ILSight.App.Tests;

/// <summary>Answers each port member of <see cref="AssemblyReloading"/> with what the test set, doing
/// <see cref="WhileWorking"/> before it answers; keeps what was asked, the tokens it was asked with, and the problems
/// reported and the refusals shown.</summary>
internal sealed class FakeAssemblyReloadingPorts : AssemblyReloading.IPorts
{
    public PdbChecking PdbChecked { get; init; } = new PdbChecking.Checked { Check = PdbCheck.Matches };

    public Func<LoadedAssembly, IReadOnlyList<NodePath>, Task<Outcome<ReopenedFile>>> ReadAgain { get; init; } =
        static (_, _) => Task.FromResult<Outcome<ReopenedFile>>(new Outcome<ReopenedFile>.Failed(new IOException("Reading again was not expected.")));

    public Func<IReadOnlyList<NodePath>, Outcome<ReloadedNodes>> Reopen { get; init; } =
        static _ => new Outcome<ReloadedNodes>.Failed(new IOException("Reopening was not expected."));

    public Action WhileWorking { get; init; } = static () => { };

    public List<string> Asked { get; } = [];

    public List<IReadOnlyList<AssemblyPath>> OpenedPaths { get; } = [];

    public List<CancellationToken> Stops { get; } = [];

    public List<CancellationToken> Tokens { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public List<Refusal> Refused { get; } = [];

    /// <summary>The modules each release was handed, in order.</summary>
    public List<RemovedModules> Released { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public void ShowRefusal(Refusal refusal)
    {
        Refused.Add(refusal);
    }

    public void ReleaseModules(RemovedModules removed)
    {
        Released.Add(removed);
        LoadedAssemblies.Release(removed);
    }

    public Task<PdbChecking> CheckPdbAsync(LoadedModule assembly, string pdbPath, CancellationToken ct)
    {
        return AnswerAsync(nameof(CheckPdbAsync), PdbChecked, ct);
    }

    public async Task<Outcome<ReopenedFile>> ReadAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct)
    {
        var read = await ReadAgain(item, places);
        return await AnswerAsync(nameof(ReadAgainAsync), read, ct);
    }

    public Task<Outcome<ReloadedNodes>> ReopenFilesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded, IReadOnlyList<NodePath> places,
        CancellationToken stop, CancellationToken ct)
    {
        OpenedPaths.Add(paths);
        Stops.Add(stop);
        return AnswerAsync(nameof(ReopenFilesAsync), Reopen(places), ct);
    }

    private Task<T> AnswerAsync<T>(string member, T answer, CancellationToken ct)
    {
        Asked.Add(member);
        Tokens.Add(ct);
        WhileWorking();
        return Task.FromResult(answer);
    }
}
