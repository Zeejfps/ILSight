namespace ILSight;

/// <summary>Something that went wrong, worth telling the user, and when.</summary>
public sealed record AppError
{
    public required AppProblem Problem { get; init; }

    public required DateTime Time { get; init; }
}
