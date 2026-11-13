using ILSight.SettingArea;

namespace ILSight.ILSightAppArea;

/// <summary>Whether the window closes as the user asks it to, from what saving the settings at that moment did: a save
/// that failed keeps it open once, the user told why, and the next request closes it, saved or not.</summary>
public sealed class WindowClosing
{
    /// <summary>Whether the last request to close was refused, the settings not saved.</summary>
    private bool _refused;

    /// <summary>Takes a request to close the window, the settings saved as it was made with
    /// <paramref name="save"/>'s outcome; true when the window may close. A failed save refuses the request, unless
    /// the request before it was refused already.</summary>
    public bool TryClose(SettingsWriteResult save)
    {
        var refuses = save.Match(_refused, static (_, _) => false, static (refusedBefore, _) => !refusedBefore);
        _refused = refuses;
        return !refuses;
    }
}
