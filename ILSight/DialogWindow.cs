using ZGF.Desktop;

namespace ILSight;

/// <summary>Where a dialog's window is on the screen, kept as it moves, and how often it came to rest
/// somewhere new, for the settings to keep.</summary>
public sealed record DialogWindow
{
    /// <summary>Where the window last came to rest.</summary>
    public WindowBounds? Resting;

    /// <summary>Where the window is; null opens it centered. A field, for the dialog to take by <c>ref</c>.</summary>
    public WindowBounds? Bounds;

    /// <summary>Counts each time the window comes to rest somewhere new: once for each move or resize,
    /// when the button comes up, never on the frames in between.</summary>
    public int Changes { get; set; }
}

public static class DialogWindows
{
    /// <summary>Puts <paramref name="window"/> where it was left in the last run.</summary>
    public static void Restore(DialogWindow window, WindowBounds? bounds)
    {
        window.Bounds = bounds;
        window.Resting = bounds;
    }

    /// <summary>Counts <paramref name="window"/> as moved once it rests where it was not before, unless
    /// <paramref name="held"/>, a button still being held, says it may move on.</summary>
    public static void Settle(DialogWindow window, bool held)
    {
        if (held)
            return;
        if (window.Bounds == window.Resting)
            return;
        window.Resting = window.Bounds;
        window.Changes++;
    }
}
