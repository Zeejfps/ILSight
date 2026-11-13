namespace ILSight;

/// <summary>The app as the composition root restores it to start, before any window runs it: the app state and the
/// screen over it.</summary>
internal sealed record RestoredApp
{
    public required AppState AppState { get; init; }

    public required UiState Screen { get; init; }
}
