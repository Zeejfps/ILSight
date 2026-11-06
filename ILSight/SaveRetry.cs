namespace ILSight;

/// <summary>Whether writing the settings is failing. A failure is reported once until a write
/// succeeds, and the autosave waits <see cref="DelaySeconds"/> before trying again, from the first
/// frame that asks after the failure.</summary>
public abstract record SaveRetry
{
    public const double DelaySeconds = 2;

    private SaveRetry()
    {
    }

    /// <summary>Writes are going through.</summary>
    public sealed record Clear : SaveRetry;

    /// <summary>A write failed, and no frame has asked since.</summary>
    public sealed record Failed : SaveRetry;

    /// <summary>A write failed, and the wait for the next try began at <see cref="Since"/>.</summary>
    public sealed record Waiting(double Since) : SaveRetry;
}
