using ILSight.Core;
using ILSight.SettingArea;

namespace ILSight.App.Tests;

/// <summary>Reads no disk: answers with the text the test gives, or none, and keeps an unreadable file as the test
/// says, copied beside it until told otherwise; counts the files it was asked to keep.</summary>
internal sealed class FakeSettingsReadingPorts : SettingsReading.IPorts
{
    public SettingsFileText Answer { get; set; } = new SettingsFileText.None();

    public SettingsKept KeepAnswer { get; set; } = new SettingsKept.CopiedTo("settings.unreadable.json");

    public List<FullFilePath> Kept { get; } = [];

    public SettingsFileText ReadSettingsText(FullFilePath file)
    {
        return Answer;
    }

    public SettingsKept KeepUnreadable(FullFilePath file)
    {
        Kept.Add(file);
        return KeepAnswer;
    }
}
