using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Decompiles whatever it is asked to when the test says, lets the cancellation of the token it was
/// asked with through, as the adapter does; keeps what it was asked and the problems reported.</summary>
internal sealed class FakeDecompileIntoTabHandlerPorts : DecompileIntoTabHandler.IPorts
{
    public List<DecompileRequest> Requests { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<Outcome<ModuleRead<NodeContent>>> DecompileAsync(BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        CancellationToken ct)
    {
        var answer = new TaskCompletionSource<Outcome<ModuleRead<NodeContent>>>();
        ct.Register(() => answer.TrySetCanceled(ct));
        Requests.Add(new DecompileRequest { Node = node, Language = language, Answer = answer });
        return answer.Task;
    }
}
