using static ILSight.AssemblyArea.Assemblies;
using static ILSight.AssemblyListArea.AssemblyLists;

namespace ILSight.ILSightAppArea;

/// <summary>Doing what the arguments of a run ask: those the app starts with, and those later runs hand the
/// window already open; and what to tell of the ones ignored, which the edge that takes them tells.</summary>
public static class RunArgumentsActions
{
    /// <summary>Decompiles to the language <paramref name="arguments"/> name, and starts with the assembly list they
    /// name, emptied first when they ask for that.</summary>
    public static void ApplyStartupOptions(AppState appState, RunArguments arguments)
    {
        if (arguments.Language is not null)
        {
            appState.Decompiler.UseLanguage(arguments.Language.Value);
        }

        if (arguments.AssemblyList is not null)
        {
            StartWithAssemblyList(appState, arguments.AssemblyList);
        }

        if (arguments.ClearList)
        {
            EmptyActiveAssemblyList(appState);
        }
    }

    /// <summary>What to tell the user of the arguments of <paramref name="arguments"/> that were ignored, or null when
    /// none was.</summary>
    public static AppProblem? FindIgnored(RunArguments arguments)
    {
        if (arguments.Ignored.Count == 0)
        {
            return null;
        }

        return new AppProblem.ArgumentsIgnored(arguments.Ignored.ToList());
    }

    /// <summary>Opens <paramref name="files"/>, then shows the namespace, type or member they
    /// ask to navigate to, or else the last file opened. Returns the load that started in the tab showing it.</summary>
    public static Task<DecompileTicket?> OpenRequestedFilesAsync(AppState appState, FilesToOpen files, CancellationToken ct)
    {
        if (files.NavigateTo is null)
        {
            return appState.AssemblyOpening.OpenAssembliesAsync(appState, files.Paths, ct);
        }
        return appState.OpenAndShowIdHandler.HandleAsync(appState, files.Paths, files.NavigateTo, ct);
    }
}
