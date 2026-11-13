namespace ILSight;

/// <summary>The revision of a log last seen, so that each change to the log is taken once.</summary>
public sealed class SeenRevision
{
    private int _revision;

    /// <summary>Takes <paramref name="revision"/> as seen; false when it was already the one last seen.</summary>
    public bool TryTake(int revision)
    {
        if (revision == _revision)
        {
            return false;
        }

        _revision = revision;
        return true;
    }
}
