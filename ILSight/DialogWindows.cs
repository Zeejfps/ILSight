using ZGF.Desktop;

namespace ILSight;

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
        {
            return;
        }

        if (window.Bounds == window.Resting)
        {
            return;
        }

        window.Resting = window.Bounds;
        window.Changes++;
    }
}
