namespace ILSight.App.Tests;

/// <summary>Typed text settles once it has stayed the same for the delay asked; picking or settling it skips the wait.</summary>
public sealed class TypedTextTests
{
    private const double Delay = 0.3;

    [Fact]
    public void CalculateSecondsToSettle_NothingNoted_IsZero()
    {
        var typed = new TypedText();

        Assert.Equal("", typed.Text);
        Assert.Equal(0, typed.CalculateSecondsToSettle(0, Delay));
    }

    [Fact]
    public void Note_NewText_WaitsTheDelayFromThen()
    {
        var typed = new TypedText();

        typed.Note("abc", 10);

        Assert.Equal("abc", typed.Text);
        Assert.Equal(Delay, typed.CalculateSecondsToSettle(10, Delay), 9);
        Assert.Equal(0.1, typed.CalculateSecondsToSettle(10.2, Delay), 9);
        Assert.Equal(0, typed.CalculateSecondsToSettle(10.3, Delay));
        Assert.Equal(0, typed.CalculateSecondsToSettle(11, Delay));
    }

    [Fact]
    public void Note_SameText_KeepsWhenItChanged()
    {
        var typed = new TypedText();
        typed.Note("abc", 10);

        typed.Note("abc", 10.2);

        Assert.Equal(0.1, typed.CalculateSecondsToSettle(10.2, Delay), 9);
    }

    [Fact]
    public void Note_ChangedAgain_RestartsTheWait()
    {
        var typed = new TypedText();
        typed.Note("abc", 10);

        typed.Note("abcd", 10.2);

        Assert.Equal("abcd", typed.Text);
        Assert.Equal(Delay, typed.CalculateSecondsToSettle(10.2, Delay), 9);
    }

    [Fact]
    public void Settle_JustTyped_IsSettledAtOnce()
    {
        var typed = new TypedText();
        typed.Note("abc", 10);

        typed.Settle();

        Assert.Equal("abc", typed.Text);
        Assert.Equal(0, typed.CalculateSecondsToSettle(10, Delay));
    }

    [Fact]
    public void Settle_WithText_TakesItInSettled()
    {
        var typed = new TypedText();
        typed.Note("abc", 10);

        typed.Settle("https://example.org/v3/index.json");

        Assert.Equal("https://example.org/v3/index.json", typed.Text);
        Assert.Equal(0, typed.CalculateSecondsToSettle(10, Delay));
    }

    [Fact]
    public void Note_TextTakenInBySettle_StaysSettled()
    {
        var typed = new TypedText();
        typed.Settle("abc");

        typed.Note("abc", 10);

        Assert.Equal(0, typed.CalculateSecondsToSettle(10, Delay));
    }
}
