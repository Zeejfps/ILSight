using ILSight.Core;
using ILSight.AssemblyArea;

namespace ILSight.App.Tests;

/// <summary>Answers each port member of <see cref="AssemblyOpening"/> with what the test set, doing
/// <see cref="WhileWorking"/> before it answers, and letting through the cancellation of the token it was asked with,
/// as the adapter does; keeps what was asked, the tokens it was asked with, and the problems reported.</summary>
internal sealed class FakeAssemblyOpeningPorts : AssemblyOpening.IPorts
{
    public BrowseNode? Opened { get; init; }

    public Outcome<List<LoadedModule>> ReferencesOpened { get; init; } = new Outcome<List<LoadedModule>>.Ok([]);

    public IReadOnlyCollection<string> Files { get; init; } = [];

    public Action WhileWorking { get; init; } = static () => { };

    public List<string> Asked { get; } = [];

    public List<IReadOnlyList<AssemblyPath>> OpenedPaths { get; } = [];

    public List<CancellationToken> Tokens { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public List<Refusal> Refused { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public void ShowRefusal(Refusal refusal)
    {
        Refused.Add(refusal);
    }

    public bool IsFile(string path)
    {
        return Files.Contains(path);
    }

    public Task<BrowseNode?> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, GenerationWork work, CancellationToken ct)
    {
        OpenedPaths.Add(paths);
        return AnswerAsync(nameof(OpenFilesAsync), Opened, ct);
    }

    public Task<Outcome<List<LoadedModule>>> OpenReferencesAsync(LoadedModule assembly, GenerationWork work, CancellationToken ct)
    {
        return AnswerAsync(nameof(OpenReferencesAsync), ReferencesOpened, ct);
    }

    private Task<T> AnswerAsync<T>(string member, T answer, CancellationToken ct)
    {
        Asked.Add(member);
        Tokens.Add(ct);
        WhileWorking();
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(answer);
    }
}
