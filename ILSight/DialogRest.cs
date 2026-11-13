using ZGF.Desktop;

namespace ILSight;

/// <summary>Where a dialog's window last came to rest, and how often it came to rest somewhere new.</summary>
public sealed class DialogRest
{
    private WindowBounds? _resting;

    /// <summary>Counts each time the window comes to rest somewhere new: once for each move or resize,
    /// when the button comes up, never on the frames in between.</summary>
    public int Changes { get; private set; }

    /// <summary>Takes <paramref name="bounds"/>, where the window was left in the last run, as where it rests,
    /// without counting a change.</summary>
    public void Restore(WindowBounds? bounds)
    {
        _resting = bounds;
    }

    /// <summary>Counts the window as moved once it rests at <paramref name="bounds"/>, where it was not before,
    /// unless <paramref name="held"/>, a button still being held, says it may move on.</summary>
    public void Settle(WindowBounds? bounds, bool held)
    {
        if (held)
        {
            return;
        }

        if (bounds == _resting)
        {
            return;
        }

        _resting = bounds;
        Changes++;
    }
}
