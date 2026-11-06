namespace ILSight;

/// <summary>A tab reopened from what the settings kept of it, <see cref="Record"/>, while it was at
/// <see cref="Version"/>.</summary>
public sealed record ReopenedTab(DocumentTab Tab, DocumentRecord Record, int Version)
{
    /// <summary>Whether the tab still waits for what it was reopened to show: it was not pointed elsewhere since.</summary>
    public bool IsWaiting => Tab.Version == Version;
}

/// <summary>The tabs reopened from the open list's saved documents that wait for what they showed to be
/// found again, each with what the settings kept of it. A tab pointed elsewhere since waits no more.</summary>
public sealed class ReopenedTabs
{
    /// <summary>The tabs waiting, each once, in the order reopened; see <see cref="SavedDocuments"/>.</summary>
    public readonly List<ReopenedTab> Tabs = new();

    /// <summary>How many searches for the nodes the tabs wait for are under way.</summary>
    public int Finding;

    /// <summary>True while the nodes the tabs waited for are being found.</summary>
    public bool IsFinding => Finding > 0;
}
