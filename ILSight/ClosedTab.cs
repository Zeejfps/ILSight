namespace ILSight;

/// <summary>What closing a tab did (<see cref="Docking.CloseTab"/>): whether it was the active tab, which leaves
/// none active until the view focuses another.</summary>
public readonly record struct ClosedTab
{
    public required bool WasActive { get; init; }
}
