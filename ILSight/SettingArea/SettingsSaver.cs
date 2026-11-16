using ILSight.Core;
using ILSight.AppSettingArea;
using ILSight.AssemblyListArea;

namespace ILSight.SettingArea;

/// <summary>Saves the settings through <see cref="IPorts"/>: when something in them changed, unless the assemblies are
/// reloading or a failed write waits to be tried again, and whenever asked. A failed write is reported, unless writes
/// were failing already, and tried again once a wait has gone by.</summary>
public sealed class SettingsSaver(SettingsSaver.IPorts ports)
{
    private static readonly SettingsSave.Unchanged Unchanged = new();

    public interface IPorts
    {
        /// <summary>Tells the user the settings could not be saved.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Writes <paramref name="text"/> to the settings file at <paramref name="file"/>, replacing it whole
        /// so a failed write never leaves it truncated; a file that could not be written comes back as
        /// <see cref="SettingsWriteResult.Failed"/>, naming it and saying why.</summary>
        SettingsWriteResult WriteSettingsFile(FullFilePath file, string text);
    }

    /// <summary>Saves the settings when what they are saved from changed since the last save written,
    /// unless the assemblies are reloading or a failed write is still waiting to be tried again at
    /// <paramref name="now"/>.</summary>
    public SettingsSave SaveIfChanged(AppState appState, TimeSpan now)
    {
        if (appState.ListSession.Reload.IsRunning)
        {
            return Unchanged;
        }

        var sources = Settings.CaptureSavedSources(appState);
        if (sources == appState.LastSave.Sources)
        {
            return Unchanged;
        }

        var wait = appState.LastSave.AdvanceRetry(now);
        if (wait > TimeSpan.Zero)
        {
            return new SettingsSave.Waiting(wait);
        }

        return Save(appState).Match<(AppState appState, TimeSpan now), SettingsSave>((appState, now),
            static (_, _) => new SettingsSave.Saved(),
            static (s, failed) => new SettingsSave.Failed
            {
                Reason = failed.Reason,
                RetryIn = s.appState.LastSave.AdvanceRetry(s.now),
            });
    }

    /// <summary>Writes the settings, counting what they were copied from as saved only once the write
    /// succeeds. A failed write is reported, unless writes were failing already.</summary>
    public SettingsWriteResult Save(AppState appState)
    {
        AssemblyLists.RememberActiveAssemblyList(appState.Lists, appState.ListSession, appState.KeptScreen.View);
        var sources = Settings.CaptureSavedSources(appState);
        var write = WriteSettings(Settings.CreateSettings(appState));
        TakeWrite(appState, sources, write);
        return write;
    }

    /// <summary>Writes <paramref name="settings"/> where they were read from; writes nothing while they are kept in
    /// memory only or may not be saved over the file they were read from.</summary>
    public SettingsWriteResult WriteSettings(AppSettings settings)
    {
        var file = settings.FilePath;
        if (file is null || !settings.CanSaveOverFile)
        {
            return new SettingsWriteResult.Written();
        }

        return ports.WriteSettingsFile(file, SettingsJson.ToJson(settings));
    }

    private void TakeWrite(AppState appState, SavedSources sources, SettingsWriteResult write)
    {
        write.Switch(
            _ => appState.LastSave.TakeWritten(sources),
            failed => ReportWriteFailure(appState, failed.Reason));
    }

    private void ReportWriteFailure(AppState appState, string error)
    {
        if (!appState.LastSave.TryStartFailing())
        {
            return;
        }

        ports.ReportError(new AppProblem.SettingsNotSaved(error));
    }
}
