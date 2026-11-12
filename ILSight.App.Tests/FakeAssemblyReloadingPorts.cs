using ILSight.Core;
using ILSight.AssemblyHandling;

namespace ILSight.App.Tests;

/// <summary>Answers each port member of <see cref="AssemblyReloading"/> with what the test set, doing
/// <see cref="WhileWorking"/> before it answers; keeps what was asked, the tokens it was asked with, and the problems
/// reported.</summary>
internal sealed class FakeAssemblyReloadingPorts : AssemblyReloading.IPorts
{
    public Outcome<PdbCheck> PdbChecked { get; init; } = new Outcome<PdbCheck>.Ok(PdbCheck.Matches);

    public Func<LoadedAssembly, IReadOnlyList<NodePath>, Outcome<ReopenedFile>> ReadAgain { get; init; } =
        static (_, _) => new Outcome<ReopenedFile>.Failed(new IOException("Reading again was not expected."));

    public Func<IReadOnlyList<NodePath>, Outcome<ReloadedNodes>> Reopen { get; init; } =
        static _ => new Outcome<ReloadedNodes>.Failed(new IOException("Reopening was not expected."));

    public Action WhileWorking { get; init; } = static () => { };

    public List<string> Asked { get; } = [];

    public List<IReadOnlyList<AssemblyPath>> OpenedPaths { get; } = [];

    public List<CancellationToken> Stops { get; } = [];

    public List<CancellationToken> Tokens { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<Outcome<PdbCheck>> CheckPdbAsync(LoadedModule assembly, string pdbPath, CancellationToken ct)
    {
        return AnswerAsync(nameof(CheckPdbAsync), PdbChecked, ct);
    }

    public Task<Outcome<ReopenedFile>> ReadAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct)
    {
        return AnswerAsync(nameof(ReadAgainAsync), ReadAgain(item, places), ct);
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
