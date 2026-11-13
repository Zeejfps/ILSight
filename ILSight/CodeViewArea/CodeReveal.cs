namespace ILSight.CodeViewArea;

/// <summary>Where a code view is asked to go once it is laid out (<see cref="PendingScroll"/>), and the line it
/// flashes once it got there.</summary>
internal sealed class CodeReveal
{
    public PendingScroll? Pending { get; private set; }

    public CodeFlash? Flash { get; private set; }

    /// <summary>Asks to go where <paramref name="pending"/> says, over whatever was asked before.</summary>
    public void Ask(PendingScroll pending)
    {
        Pending = pending;
    }

    /// <summary>Asks to go where <paramref name="pending"/> says, unless something else was asked already.</summary>
    public void AskUnlessAsked(PendingScroll pending)
    {
        if (Pending is not null)
        {
            return;
        }

        Pending = pending;
    }

    /// <summary>Takes what was asked as done.</summary>
    public void Arrive()
    {
        Pending = null;
    }

    /// <summary>Takes what was asked as done, flashing <paramref name="line"/> from <paramref name="now"/>.</summary>
    public void ArriveFlashing(int line, double now)
    {
        Flash = new CodeFlash { Line = line, From = now };
        Pending = null;
    }

    public void EndFlash()
    {
        Flash = null;
    }

    /// <summary>Drops a match asked to be shown and the flash, which a new document makes stale; a position or
    /// line asked for stays, for the new document.</summary>
    public void ForgetStale()
    {
        if (Pending is PendingScroll.ShowMatch)
        {
            Pending = null;
        }

        Flash = null;
    }
}
