namespace ILSight;

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
