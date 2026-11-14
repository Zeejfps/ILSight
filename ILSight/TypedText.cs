namespace ILSight;

/// <summary>Text the user types, and when it last changed, so work on it can wait until typing pauses.</summary>
public sealed class TypedText
{
    // Null once the text counts as settled.
    private TimeSpan? _changedAt;

    /// <summary>The text as last noted; empty before any.</summary>
    public string Text { get; private set; } = "";

    /// <summary>Takes in <paramref name="text"/> as it is at <paramref name="now"/>; the time counts only
    /// when it differs from <see cref="Text"/>. Runs every frame, so allocates nothing.</summary>
    public void Note(string text, TimeSpan now)
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
        _changedAt = null;
    }

    /// <summary>Takes in <paramref name="text"/> as settled already, as when picked rather than typed.</summary>
    public void Settle(string text)
    {
        Text = text;
        Settle();
    }

    /// <summary>The time left at <paramref name="now"/> before <see cref="Text"/> has stayed the same for
    /// <paramref name="delay"/>, or zero once it has.</summary>
    public TimeSpan CalculateTimeToSettle(TimeSpan now, TimeSpan delay)
    {
        if (_changedAt is null)
        {
            return TimeSpan.Zero;
        }

        var left = delay - (now - _changedAt.Value);
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }
}
