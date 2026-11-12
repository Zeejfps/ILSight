using ILSight.Core;
using ILSight.SettingsKeeping;

namespace ILSight.App.Tests;

public sealed class SettingsReadingTests
{
    private static readonly FullFilePath SettingsFile = FullFilePath.Of(Path.Combine(Path.GetTempPath(), "settings.json"));

    [Fact]
    public void ReadFile_NoFile_GivesTheDefaultsToSaveOverItAndKeepsNothing()
    {
        var ports = new FakeSettingsReadingPorts();

        var (settings, problem) = new SettingsReading(ports).ReadFile(SettingsFile);

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.Equal(SettingsFile, settings.FilePath);
        Assert.True(settings.CanSaveOverFile);
        Assert.Empty(ports.Kept);
    }

    [Fact]
    public void ReadFile_NotRead_KeepsTheFileBeforeSavingOverIt()
    {
        var ports = new FakeSettingsReadingPorts { Answer = new SettingsFileText.NotRead("Locked.") };

        var (settings, problem) = new SettingsReading(ports).ReadFile(SettingsFile);

        var notRead = Assert.IsType<SettingsProblem.NotRead>(problem);
        Assert.Equal("Locked.", notRead.Detail);
        Assert.Equal(new SettingsKept.CopiedTo("settings.unreadable.json"), notRead.Kept);
        Assert.True(settings.CanSaveOverFile);
        Assert.Equal([SettingsFile], ports.Kept);
    }

    [Fact]
    public void ReadFile_NotJsonAndNotKept_DoesNotSaveOverIt()
    {
        var ports = new FakeSettingsReadingPorts { Answer = new SettingsFileText.Text("{ not json"), KeepAnswer = new SettingsKept.NotSaved() };

        var (settings, problem) = new SettingsReading(ports).ReadFile(SettingsFile);

        Assert.IsType<SettingsKept.NotSaved>(Assert.IsType<SettingsProblem.NotJson>(problem).Kept);
        Assert.False(settings.CanSaveOverFile);
    }

    [Fact]
    public void ReadFile_ValueLeftOut_KeepsTheFileAndReadsTheRest()
    {
        var ports = new FakeSettingsReadingPorts { Answer = new SettingsFileText.Text("""{ "Language": 42, "CheckForUpdates": false }""") };

        var (settings, problem) = new SettingsReading(ports).ReadFile(SettingsFile);

        Assert.Equal(["Language"], Assert.IsType<SettingsProblem.ValuesLeftOut>(problem).Fields);
        Assert.False(settings.CheckForUpdates);
        Assert.Equal([SettingsFile], ports.Kept);
    }

    [Fact]
    public void ReadFile_EveryValueRead_KeepsNothing()
    {
        var ports = new FakeSettingsReadingPorts { Answer = new SettingsFileText.Text("""{ "CheckForUpdates": false }""") };

        var (settings, problem) = new SettingsReading(ports).ReadFile(SettingsFile);

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.False(settings.CheckForUpdates);
        Assert.Empty(ports.Kept);
    }
}
