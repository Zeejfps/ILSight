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
