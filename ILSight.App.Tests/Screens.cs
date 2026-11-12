using ILSight.AppSettingArea;

namespace ILSight.App.Tests;

internal static class Screens
{
    /// <summary>A screen over an app state with nothing open, for drawing a widget alone.</summary>
    public static UiState Blank()
    {
        return AppRoot.CreateScreen(AppRoot.RestoreAppState(new AppSettings { Locale = Locale.En }, TestSession.Shared));
    }
}
