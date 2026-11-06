namespace ILSight;

/// <summary>What the screen asks of the app state, which <see cref="UiState"/> queues while it draws
/// and <see cref="ILSightApp"/> runs, in the order queued, once the frame is drawn. Made through
/// <see cref="UiState.Later(AppCommand)"/> and its overload, from a <c>static</c> lambda handed what it
/// acts on, so that a view drawn every frame allocates nothing until it asks.</summary>
public delegate void AppCommand(AppState appState);

public static class Commands
{
    /// <summary>A command doing <paramref name="run"/> to <paramref name="subject"/>. It is made here, so
    /// that a caller that only sometimes asks doesn't allocate a closure on the way in every time.</summary>
    public static AppCommand On<T>(T subject, Action<AppState, T> run)
    {
        return appState =>
        {
            run(appState, subject);
        };
    }
}
