namespace ILSight;

/// <summary>The tabs reopened from the open list's saved documents that wait for what they showed to be
/// found again, each with what the settings kept of it. A tab pointed elsewhere since waits no more.</summary>
public sealed class ReopenedTabs
{
    /// <summary>The tabs waiting, each once, in the order reopened; see <see cref="SavedDocuments"/>.</summary>
    public readonly List<ReopenedTab> Tabs = new();
}
