using ILSight.Core;
using ILSight.SettingsFormat;

namespace ILSight.App.Tests;

internal static class TempSettings
{
    public static AppSettings Settings(this TempDir temp)
    {
        return SettingsJson.ReadSettingsFile(FullFilePath.Of(temp.PathOf("settings.json"))).Settings;
    }
}
