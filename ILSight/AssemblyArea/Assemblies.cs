using ILSight.Core;

namespace ILSight.AssemblyArea;

/// <summary>What to tell the user about opening assemblies that failed. Opening them is
/// <see cref="AssemblyOpening"/>'s, reading them again <see cref="AssemblyReloading"/>'s, closing them the open list
/// session's (<see cref="OpenListSession"/>).</summary>
public static class Assemblies
{
    /// <summary>What to tell the user about opening that failed with <paramref name="error"/>; null when the
    /// opening was cancelled, which is no failure.</summary>
    internal static AppProblem.OpenFailed? FindOpenProblem(Exception error)
    {
        if (error is OperationCanceledException)
        {
            return null;
        }

        return new AppProblem.OpenFailed(error);
    }
}
