namespace ILSight;

/// <summary>Text the user types, and when it last changed, so work on it can wait until typing pauses.</summary>
public sealed class TypedText
{
    private double _changedAt = double.NegativeInfinity;

    /// <summary>The text as last noted; empty before any.</summary>
    public string Text { get; private set; } = "";

    /// <summary>Takes in <paramref name="text"/> as it is at <paramref name="now"/>; the time counts only
    /// when it differs from <see cref="Text"/>. Runs every frame, so allocates nothing.</summary>
    public void Note(string text, double now)
    {
        if (text == Text)
        {
            return;
        }

        Text = text;
        _changedAt = now;
    }

    /// <summary>Counts <see cref="Text"/> as settled already, so work on it needn't wait.</summary>
    public void Settle()
    {
        _changedAt = double.NegativeInfinity;
    }

    /// <summary>Takes in <paramref name="text"/> as settled already, as when picked rather than typed.</summary>
    public void Settle(string text)
    {
        Text = text;
        Settle();
    }

    /// <summary>The seconds left at <paramref name="now"/> before <see cref="Text"/> has stayed the same for
    /// <paramref name="delay"/> seconds, or 0 once it has.</summary>
    public double CalculateSecondsToSettle(double now, double delay)
    {
        return Math.Max(0, delay - (now - _changedAt));
    }
}
