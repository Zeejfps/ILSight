using ILSight.Core;
using ILSight.Core.Comparison;
using ILSight.AssemblyArea;

namespace ILSight.App.Tests;

/// <summary>Compares whatever it is asked to when the test says, lets the cancellation of the token it was asked
/// with through, as the adapter does; keeps the problems reported.</summary>
internal sealed class FakeCompareAssembliesHandlerPorts : CompareAssembliesHandler.IPorts
{
    public List<TaskCompletionSource<Outcome<ModuleRead<ApiDiff>>>> Answers { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<Outcome<ModuleRead<ApiDiff>>> CompareAsync(LoadedModule left, LoadedModule right, ApiScope scope, CancellationToken ct)
    {
        var answer = new TaskCompletionSource<Outcome<ModuleRead<ApiDiff>>>();
        ct.Register(() => answer.TrySetCanceled(ct));
        Answers.Add(answer);
        return answer.Task;
    }
}
