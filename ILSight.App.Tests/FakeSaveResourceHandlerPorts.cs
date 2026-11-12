using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Writes nothing, answering as the test says; keeps the problems reported.</summary>
internal sealed class FakeSaveResourceHandlerPorts : SaveResourceHandler.IPorts
{
    public required Outcome Answer { get; init; }

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Outcome SaveResource(BrowseNode node, string path)
    {
        return Answer;
    }
}
