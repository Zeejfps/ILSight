namespace ILSight;

/// <summary>A question the screen asks before an action it can't undo, shown until it is answered or dismissed.</summary>
public sealed class Confirmation
{
    public bool IsAsked { get; private set; }

    public void Ask()
    {
        IsAsked = true;
    }

    public void Stop()
    {
        IsAsked = false;
    }
}
