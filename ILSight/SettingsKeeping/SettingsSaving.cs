using System.Diagnostics;
using ILSight.Core;
using ILSight.AppSettingArea;
using static ILSight.AssemblyListArea.AssemblyLists;

namespace ILSight.SettingsKeeping;

/// <summary>Saves the settings through <see cref="IPorts"/>: when something in them changed, unless the assemblies are
/// reloading or a failed write waits to be tried again, and whenever asked. A failed write is reported, unless writes
/// were failing already, and tried again once a wait has gone by.</summary>
public sealed class SettingsSaving(SettingsSaving.IPorts ports)
{
    private static readonly Saving.Unchanged Unchanged = new();

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
    public Saving SaveIfChanged(AppState appState, double now)
    {
        if (appState.IsReloading)
        {
            return Unchanged;
        }

        var sources = Settings.CaptureSavedSources(appState);
        if (sources == appState.LastSaved)
        {
            return Unchanged;
        }

        var wait = AdvanceRetry(appState, now);
        if (wait > 0)
        {
            return new Saving.Waiting(wait);
        }

        return Save(appState) switch
        {
            SettingsWriteResult.Written => new Saving.Saved(),
            SettingsWriteResult.Failed failed => new Saving.Failed(failed.Reason, AdvanceRetry(appState, now)),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>Moves the wait on, starting it if this is the first time it is asked for after a failure,
    /// and returns the seconds left at <paramref name="now"/> before a failed write is tried again.</summary>
    private static double AdvanceRetry(AppState appState, double now)
    {
        switch (appState.SaveRetry)
        {
            case SaveRetry.Clear:
                return 0;
            case SaveRetry.Failed:
                appState.SaveRetry = new SaveRetry.Waiting(now);
                return SaveRetry.DelaySeconds;
            case SaveRetry.Waiting waiting:
                return Math.Max(0, SaveRetry.DelaySeconds - (now - waiting.Since));
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Writes the settings, counting what they were copied from as saved only once the write
    /// succeeds. A failed write is reported, unless writes were failing already.</summary>
    public SettingsWriteResult Save(AppState appState)
    {
        RememberActiveAssemblyList(appState.Lists, appState.ListSession, appState.View);
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
        switch (write)
        {
            case SettingsWriteResult.Written:
                appState.LastSaved = sources;
                appState.SaveRetry = new SaveRetry.Clear();
                break;
            case SettingsWriteResult.Failed failed:
                ReportWriteFailure(appState, failed.Reason);
                break;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Reports a failed write, unless writes were failing already.</summary>
    private void ReportWriteFailure(AppState appState, string error)
    {
        var wasFailing = appState.SaveRetry is not SaveRetry.Clear;
        appState.SaveRetry = new SaveRetry.Failed();
        if (wasFailing)
        {
            return;
        }

        ports.ReportError(new AppProblem.SettingsNotSaved(error));
    }
}
