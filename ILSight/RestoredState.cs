namespace ILSight;

/// <summary>The app state as the composition root restores it to start with no screen, and the start recorded on it
/// (the active list's reopening begun, nothing read), which the host starts
/// (<see cref="ILSightAppArea.AppStart.Start"/>).</summary>
internal sealed record RestoredState
{
    public required AppState AppState { get; init; }

    public required ListStart Start { get; init; }
}
