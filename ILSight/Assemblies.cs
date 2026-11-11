using ILSight.Core;
using static ILSight.SavedDocuments;

namespace ILSight;

/// <summary>Closing the assemblies of the list, and what to tell the user about opening that failed. Opening them
/// is <see cref="AssemblyOpening"/>'s, reading them again <see cref="AssemblyReloading"/>'s.</summary>
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

    /// <summary>Takes <paramref name="items"/> off <paramref name="list"/>; one no longer on it is passed over.</summary>
    public static void CloseAssemblies(AssemblyList list, IReadOnlyList<LoadedAssembly> items)
    {
        foreach (var item in items)
        {
            AssemblyListing.CloseAssembly(list, item);
        }
    }

    /// <summary>Takes the files that failed to load off <paramref name="list"/>.</summary>
    public static void CloseFailedAssemblies(AssemblyList list)
    {
        var failed = list.Listed
            .OfType<FailedAssembly>()
            .ToList();
        foreach (var assembly in failed)
        {
            AssemblyListing.CloseAssembly(list, assembly);
        }
    }

    /// <summary>Closes every assembly of the open list, stopping its reload; tabs still waiting for it to
    /// reopen their places show what is left of them.</summary>
    public static void CloseAllAssemblies(AppState appState)
    {
        appState.Intent.Begin();
        appState.Opens.Cancel();
        appState.Reload.Stop();
        ClearAssemblies(appState);
        appState.Assemblies.Pdbs.ForgetAll();
        appState.Assemblies.TargetFrameworks.ForgetAll();
        ShowReopenedDocuments(appState);
    }

    /// <summary>Takes every assembly off the open list, and the analyses of them with it.</summary>
    internal static void ClearAssemblies(AppState appState)
    {
        AssemblyListing.CloseAll(appState.Assemblies);
        appState.Analyzer.ClearRoots();
    }
}
