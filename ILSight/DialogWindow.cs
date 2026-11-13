using ZGF.Desktop;

namespace ILSight;

/// <summary>Where a dialog's window is on the screen, kept as it moves, and where it came to rest, for the settings
/// to keep.</summary>
public sealed class DialogWindow
{
    /// <summary>Where the window is; null opens it centered. A field, for the dialog to take by <c>ref</c>.</summary>
    public WindowBounds? Bounds;

    public DialogRest Rest { get; } = new();

    /// <summary>Puts the window where it was left in the last run.</summary>
    public void Restore(WindowBounds? bounds)
    {
        Bounds = bounds;
        Rest.Restore(bounds);
    }

    /// <summary>Tells <see cref="Rest"/> where the window is now, unless <paramref name="held"/>, a button still
    /// being held, says it may move on.</summary>
    public void Settle(bool held)
    {
        Rest.Settle(Bounds, held);
    }
}
