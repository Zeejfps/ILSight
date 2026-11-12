
namespace ILSight.App.Tests;

internal static class AppErrors
{
    /// <summary>The error in English words.</summary>
    public static ProblemText Words(this AppError error)
    {
        return ProblemTexts.Describe(Strings.En, error.Problem);
    }
}
