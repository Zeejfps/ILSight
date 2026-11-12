using ZGF.Desktop;

namespace ILSight;

/// <summary>Where a dialog's window is on the screen, kept as it moves, and how often it came to rest
/// somewhere new, for the settings to keep.</summary>
public sealed class DialogWindow
{
    /// <summary>Where the window last came to rest.</summary>
    public WindowBounds? Resting;

    /// <summary>Where the window is; null opens it centered. A field, for the dialog to take by <c>ref</c>.</summary>
    public WindowBounds? Bounds;

    /// <summary>Counts each time the window comes to rest somewhere new: once for each move or resize,
    /// when the button comes up, never on the frames in between.</summary>
    public int Changes { get; set; }
}
