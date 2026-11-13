using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>When the caret last moved, and where to: it shows from then for half a blink.</summary>
internal sealed class CaretBlink
{
    private TextPosition _caret;

    /// <summary>When the caret last moved.</summary>
    public double From { get; private set; }

    /// <summary>Starts the blink over at <paramref name="now"/> when <paramref name="caret"/> is not where the caret
    /// was.</summary>
    public void Follow(TextPosition caret, double now)
    {
        if (caret == _caret)
        {
            return;
        }

        _caret = caret;
        From = now;
    }
}
