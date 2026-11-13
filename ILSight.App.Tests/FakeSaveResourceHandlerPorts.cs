using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Writes nothing, answering as the test says; keeps the problems reported and the refusals shown.</summary>
internal sealed class FakeSaveResourceHandlerPorts : SaveResourceHandler.IPorts
{
    public required ResourceSave Answer { get; init; }

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

    public ResourceSave SaveResource(BrowseNode node, string path)
    {
        return Answer;
    }
}
