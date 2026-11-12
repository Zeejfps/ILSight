namespace ILSight;

/// <summary>A tab reopened from what the settings kept of it, <see cref="Record"/>, while it was at
/// <see cref="Version"/>.</summary>
public sealed record ReopenedTab
{
    public required DocumentTab Tab { get; init; }

    public required DocumentRecord Record { get; init; }

    public required int Version { get; init; }

    /// <summary>Whether the tab still waits for what it was reopened to show: it was not pointed elsewhere since.</summary>
    public bool IsWaiting => Tab.Version == Version;
}
