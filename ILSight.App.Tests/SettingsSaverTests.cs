using ILSight.Core;
using ILSight.AppSettingArea;
using ILSight.SettingArea;

namespace ILSight.App.Tests;

public sealed class SettingsSaverTests
{
    private static readonly FullFilePath SettingsFile = FullFilePath.Of(Path.Combine(Path.GetTempPath(), "settings.json"));

    private static AppSettings InFile()
    {
        return new AppSettings { FilePath = SettingsFile, CanSaveOverFile = true };
    }

    [Fact]
    public void SaveIfChanged_Changed_WritesOnceAndCountsItSaved()
    {
        var d = AppStateDriver.WithoutScreen(InFile());
        var ports = new FakeSettingsSaverPorts();
        var saving = new SettingsSaver(ports);
        d.AppState.Decompiler.UseLanguage(DecompilationLanguage.IL);

        var first = saving.SaveIfChanged(d.AppState, TimeSpan.FromSeconds(0));
        var second = saving.SaveIfChanged(d.AppState, TimeSpan.FromSeconds(0));

        UnionAsserts.GetCase<SettingsSave.Saved>(first);
        UnionAsserts.GetCase<SettingsSave.Unchanged>(second);
        Assert.Equal([SettingsFile], ports.Written);
    }

    [Fact]
    public void SaveIfChanged_WriteFailed_ReportsItOnceAndWaitsToTryAgain()
    {
        var d = AppStateDriver.WithoutScreen(InFile());
        var ports = new FakeSettingsSaverPorts { Answer = new SettingsWriteResult.Failed("Disk full.") };
        var saving = new SettingsSaver(ports);
        d.AppState.Decompiler.UseLanguage(DecompilationLanguage.IL);

        var failed = saving.SaveIfChanged(d.AppState, TimeSpan.FromSeconds(0));
        var waiting = saving.SaveIfChanged(d.AppState, TimeSpan.FromSeconds(1));
        var failedAgain = saving.SaveIfChanged(d.AppState, LastSave.RetryDelay);

        Assert.Equal(LastSave.RetryDelay, UnionAsserts.GetCase<SettingsSave.Failed>(failed).RetryIn);
        Assert.Equal(LastSave.RetryDelay - TimeSpan.FromSeconds(1), UnionAsserts.GetCase<SettingsSave.Waiting>(waiting).RetryIn);
        UnionAsserts.GetCase<SettingsSave.Failed>(failedAgain);
        Assert.Equal(2, ports.Written.Count);
        Assert.Equal("Disk full.", UnionAsserts.GetCase<AppProblem.SettingsNotSaved>(Assert.Single(ports.Reported)).Reason);
    }

    [Fact]
    public void WriteSettings_KeptInMemoryOnly_WritesNothing()
    {
        var ports = new FakeSettingsSaverPorts();

        var written = new SettingsSaver(ports).WriteSettings(new AppSettings());

        UnionAsserts.GetCase<SettingsWriteResult.Written>(written);
        Assert.Empty(ports.Written);
    }

    [Fact]
    public void WriteSettings_MayNotSaveOverTheFile_WritesNothing()
    {
        var ports = new FakeSettingsSaverPorts();

        var written = new SettingsSaver(ports).WriteSettings(new AppSettings { FilePath = SettingsFile, CanSaveOverFile = false });

        UnionAsserts.GetCase<SettingsWriteResult.Written>(written);
        Assert.Empty(ports.Written);
    }
}
