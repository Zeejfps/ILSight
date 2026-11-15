using ILSight.Core;

namespace ILSight.AssemblyArea;

/// <summary>A tab whose place a reload or a reopen finds again in the file read again: the tab, the place, the version it
/// was at, so a tab moved on since is left as it is, and the position to keep.</summary>
public sealed record ReloadingTab
{
    public required DocumentTab Tab { get; init; }

    public required NodePath Location { get; init; }

    public required int Version { get; init; }

    public required ViewPosition? Position { get; init; }
}
