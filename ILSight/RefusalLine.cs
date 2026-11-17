namespace ILSight;

/// <summary>The last thing the user asked for that the app refused, said in the status bar until they dismiss it or
/// another refusal takes its place. UI thread only.</summary>
public sealed class RefusalLine
{
    public Refusal? Shown { get; private set; }

    /// <summary>How many times a refusal was shown: each show, the same refusal again among them, a new showing.</summary>
    public int Showings { get; private set; }

    public void Show(Refusal refusal)
    {
        Shown = refusal;
        Showings++;
    }

    public void Dismiss()
    {
        Shown = null;
    }
}
