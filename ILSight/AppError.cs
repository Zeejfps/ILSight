namespace ILSight;

/// <summary>Something that went wrong, worth telling the user, and when.</summary>
public sealed record AppError(AppProblem Problem, DateTime Time);
