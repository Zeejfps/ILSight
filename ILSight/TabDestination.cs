namespace ILSight;

/// <summary>Where a node asked for shows: a new tab kept open when <see cref="InNewTab"/>; else, as decided
/// when it shows, the preview tab while tabs preview, else <see cref="Origin"/>, the tab it was asked from,
/// or a new tab when that was closed meanwhile.</summary>
public readonly record struct TabDestination
{
    public required DocumentTab? Origin { get; init; }

    public required bool InNewTab { get; init; }
}
