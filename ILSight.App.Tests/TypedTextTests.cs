namespace ILSight.App.Tests;

/// <summary>Typed text settles once it has stayed the same for the delay asked; picking or settling it skips the wait.</summary>
public sealed class TypedTextTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(0.3);

    private static TimeSpan At(double seconds)
    {
        return TimeSpan.FromSeconds(seconds);
    }

    [Fact]
    public void CalculateTimeToSettle_NothingNoted_IsZero()
    {
        var typed = new TypedText();

        Assert.Equal("", typed.Text);
        Assert.Equal(TimeSpan.Zero, typed.CalculateTimeToSettle(At(0), Delay));
    }

    [Fact]
    public void Note_NewText_WaitsTheDelayFromThen()
    {
        var typed = new TypedText();

        typed.Note("abc", At(10));

        Assert.Equal("abc", typed.Text);
        Assert.Equal(Delay, typed.CalculateTimeToSettle(At(10), Delay));
        Assert.Equal(0.1, typed.CalculateTimeToSettle(At(10.2), Delay).TotalSeconds, 9);
        Assert.Equal(TimeSpan.Zero, typed.CalculateTimeToSettle(At(10.3), Delay));
        Assert.Equal(TimeSpan.Zero, typed.CalculateTimeToSettle(At(11), Delay));
    }

    [Fact]
    public void Note_SameText_KeepsWhenItChanged()
    {
        var typed = new TypedText();
        typed.Note("abc", At(10));

        typed.Note("abc", At(10.2));

        Assert.Equal(0.1, typed.CalculateTimeToSettle(At(10.2), Delay).TotalSeconds, 9);
    }

    [Fact]
    public void Note_ChangedAgain_RestartsTheWait()
    {
        var typed = new TypedText();
        typed.Note("abc", At(10));

        typed.Note("abcd", At(10.2));

        Assert.Equal("abcd", typed.Text);
        Assert.Equal(Delay, typed.CalculateTimeToSettle(At(10.2), Delay));
    }

    [Fact]
    public void Settle_JustTyped_IsSettledAtOnce()
    {
        var typed = new TypedText();
        typed.Note("abc", At(10));

        typed.Settle();

        Assert.Equal("abc", typed.Text);
        Assert.Equal(TimeSpan.Zero, typed.CalculateTimeToSettle(At(10), Delay));
    }

    [Fact]
    public void Settle_WithText_TakesItInSettled()
    {
        var typed = new TypedText();
        typed.Note("abc", At(10));

        typed.Settle("https://example.org/v3/index.json");

        Assert.Equal("https://example.org/v3/index.json", typed.Text);
        Assert.Equal(TimeSpan.Zero, typed.CalculateTimeToSettle(At(10), Delay));
    }

    [Fact]
    public void Note_TextTakenInBySettle_StaysSettled()
    {
        var typed = new TypedText();
        typed.Settle("abc");

        typed.Note("abc", At(10));

        Assert.Equal(TimeSpan.Zero, typed.CalculateTimeToSettle(At(10), Delay));
    }
}
