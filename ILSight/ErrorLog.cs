namespace ILSight;

/// <summary>Something that went wrong, worth telling the user, and when.</summary>
public sealed record AppError(AppProblem Problem, DateTime Time);

/// <summary>The recent errors, newest last, the oldest dropped past a capacity. UI thread only.</summary>
public sealed class ErrorLog
{
    public readonly List<AppError> Entries = new();

    /// <summary>Changes whenever an error is added; clearing leaves it.</summary>
    public int Revision;
}

/// <summary>Telling the user something went wrong.</summary>
public static class Errors
{
    /// <summary>Adds <paramref name="problem"/> to the errors shown, as of now.</summary>
    public static void Report(AppState appState, AppProblem problem)
    {
        Report(appState.Errors, problem);
    }

    /// <summary>Adds <paramref name="problem"/> to <paramref name="errors"/>, as of now.</summary>
    public static void Report(ErrorLog errors, AppProblem problem)
    {
        ErrorDialogView.AddError(errors, problem, DateTime.Now);
    }
}
