namespace ILSight;

/// <summary>The app as the composition root restores it to start, before any window runs it: the app state, the
/// screen over it, and the start recorded on the state (the active list's reopening begun, nothing read), which the
/// app starts once it has taken the arguments already waiting.</summary>
internal sealed record RestoredApp
{
    public required AppState AppState { get; init; }

    public required UiState Screen { get; init; }

    public required ListStart Start { get; init; }
}
