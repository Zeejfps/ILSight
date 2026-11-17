namespace ILSight;

/// <summary>What one save of the settings when they changed did.</summary>
public union SettingsSave(SettingsSave.Unchanged, SettingsSave.Saved, SettingsSave.Waiting, SettingsSave.Failed)
{
    /// <summary>Nothing changed since the last write, or the assemblies are reloading.</summary>
    public sealed record Unchanged;

    public sealed record Saved;

    /// <summary>A failed write is tried again only in <see cref="RetryIn"/>.</summary>
    public sealed record Waiting(TimeSpan RetryIn);

    /// <summary>The write failed, and is tried again in <see cref="RetryIn"/>.</summary>
    public sealed record Failed
    {
        public required string Reason { get; init; }

        public required TimeSpan RetryIn { get; init; }
    }
}
