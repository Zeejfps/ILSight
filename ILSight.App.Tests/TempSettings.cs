using ILSight.Core;
using ILSight.SettingsFormat;

namespace ILSight.App.Tests;

internal static class TempSettings
{
    public static AppSettings Settings(this TempDir temp)
    {
        return AppRoot.ReadSettingsFile(FullFilePath.Of(temp.PathOf("settings.json"))).Settings;
    }

    /// <summary>Writes <paramref name="settings"/> where they were read from, as the app saves them.</summary>
    public static SettingsWriteResult Save(AppSettings settings)
    {
        return new SettingsSaving(new SettingsSavingAdapter(new ErrorLog())).WriteSettings(settings);
    }
}
