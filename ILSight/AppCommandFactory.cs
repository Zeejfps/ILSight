namespace ILSight;

/// <summary>Makes an <see cref="AppCommand"/> doing an action to what it is handed, so that a caller that only
/// sometimes asks doesn't allocate a closure on the way in every time.</summary>
public static class AppCommandFactory
{
    /// <summary>A command doing <paramref name="run"/> to <paramref name="subject"/>. Pass a <c>static</c> lambda,
    /// handing it what it acts on as <paramref name="subject"/>.</summary>
    public static AppCommand Create<T>(T subject, Action<AppState, T> run)
    {
        return appState =>
        {
            run(appState, subject);
        };
    }
}
