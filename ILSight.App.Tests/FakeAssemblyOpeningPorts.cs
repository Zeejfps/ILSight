using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Answers each port member of <see cref="AssemblyOpening"/> with what the test set, doing
/// <see cref="WhileWorking"/> before it answers; keeps what was asked, the tokens it was asked with, and the problems
/// reported.</summary>
internal sealed class FakeAssemblyOpeningPorts : AssemblyOpening.IPorts
{
    public Outcome<LoadedAssembly?> Opened { get; init; } = new Outcome<LoadedAssembly?>.Ok(null);

    public Outcome<List<LoadedModule>> ReferencesOpened { get; init; } = new Outcome<List<LoadedModule>>.Ok([]);

    public IReadOnlyCollection<string> Files { get; init; } = [];

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

    public bool IsFile(string path)
    {
        return Files.Contains(path);
    }

    public Task<Outcome<LoadedAssembly?>> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, CancellationToken stop, CancellationToken ct)
    {
        OpenedPaths.Add(paths);
        Stops.Add(stop);
        return AnswerAsync(nameof(OpenFilesAsync), Opened, ct);
    }

    public Task<Outcome<List<LoadedModule>>> OpenReferencesAsync(LoadedModule assembly, CancellationToken stop, CancellationToken ct)
    {
        Stops.Add(stop);
        return AnswerAsync(nameof(OpenReferencesAsync), ReferencesOpened, ct);
    }

    private Task<T> AnswerAsync<T>(string member, T answer, CancellationToken ct)
    {
        Asked.Add(member);
        Tokens.Add(ct);
        WhileWorking();
        return Task.FromResult(answer);
    }
}
