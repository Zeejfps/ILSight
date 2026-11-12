using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Writes no file: answers each write as the test says, written until told otherwise; keeps the files it was
/// asked to write and the problems reported.</summary>
internal sealed class FakeSettingsSavingPorts : SettingsSaving.IPorts
{
    public SettingsWriteResult Answer { get; set; } = new SettingsWriteResult.Written();

    public List<FullFilePath> Written { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public SettingsWriteResult WriteSettingsFile(FullFilePath file, string text)
    {
        Written.Add(file);
        return Answer;
    }
}
