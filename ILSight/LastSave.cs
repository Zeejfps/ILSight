namespace ILSight;

/// <summary>What the settings were last saved from, to save again only when it changed, and whether writing them is
/// failing, and when to try again. A failure is reported once until a write succeeds.</summary>
public sealed class LastSave
{
    /// <summary>How long the autosave waits after a failed write before trying again, from the first frame that asks
    /// after the failure.</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private SaveRetry _retry = new SaveRetry.Clear();

    /// <summary>What the settings were saved from the last time.</summary>
    public SavedSources Sources { get; private set; }

    /// <summary>Counts <paramref name="restored"/>, what the settings just restored were copied into, as saved.</summary>
    public void Restore(SavedSources restored)
    {
        Sources = restored;
    }

    /// <summary>Counts <paramref name="written"/> as saved, and writes as going through.</summary>
    public void TakeWritten(SavedSources written)
    {
        Sources = written;
        _retry = new SaveRetry.Clear();
    }

    /// <summary>Notes that a write failed, the wait for the next try beginning again with the next ask; true when it
    /// starts the failing (writes were going through until then), the one failure to report.</summary>
    public bool TryStartFailing()
    {
        var wasFailing = _retry is not SaveRetry.Clear;
        _retry = new SaveRetry.Failed();
        return !wasFailing;
    }

    /// <summary>How long a failed write still waits at <paramref name="now"/> before it is tried again, the wait
    /// beginning with the first ask after the failure; zero when writes are going through.</summary>
    public TimeSpan AdvanceRetry(TimeSpan now)
    {
        return _retry switch
        {
            SaveRetry.Clear => TimeSpan.Zero,
            SaveRetry.Failed => StartWaiting(now),
            SaveRetry.Waiting waiting => CalculateWaitLeft(now, waiting.Since),
        };
    }

    private TimeSpan StartWaiting(TimeSpan now)
    {
        _retry = new SaveRetry.Waiting(now);
        return RetryDelay;
    }

    private static TimeSpan CalculateWaitLeft(TimeSpan now, TimeSpan since)
    {
        var left = RetryDelay - (now - since);
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /// <summary>Whether writing the settings is failing. A failure is reported once until a write succeeds, and the
    /// autosave waits <see cref="RetryDelay"/> before trying again, from the first frame that asks after the
    /// failure.</summary>
    private union SaveRetry(SaveRetry.Clear, SaveRetry.Failed, SaveRetry.Waiting)
    {
        // Writes are going through.
        public sealed record Clear;

        // A write failed, and no frame has asked since.
        public sealed record Failed;

        // A write failed, and the wait for the next try began at Since.
        public sealed record Waiting(TimeSpan Since);
    }
}
