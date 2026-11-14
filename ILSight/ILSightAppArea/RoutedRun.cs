namespace ILSight.ILSightAppArea;

/// <summary>Where a run's arguments went (<see cref="Route"/>), and the receiver of later runs' arguments this run holds
/// when it opens the window: null when they were handed over or the hand-over stayed unconfirmed.</summary>
public sealed record RoutedRun
{
    public required RunRoute Route { get; init; }

    public required RunArgumentsReceiver? Receiver { get; init; }
}
