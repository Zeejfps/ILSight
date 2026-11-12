namespace ILSight;

/// <summary>A tab reopened from what the settings kept of it, <see cref="Record"/>, while it was at
/// <see cref="Version"/>.</summary>
public sealed record ReopenedTab(DocumentTab Tab, DocumentRecord Record, int Version)
{
    /// <summary>Whether the tab still waits for what it was reopened to show: it was not pointed elsewhere since.</summary>
    public bool IsWaiting => Tab.Version == Version;
}
