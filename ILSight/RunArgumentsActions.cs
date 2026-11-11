using static ILSight.Assemblies;
using static ILSight.AssemblyLists;
using static ILSight.Navigation;
using static ILSight.Preferences;

namespace ILSight;

/// <summary>Doing what the arguments of a run ask: those the app starts with, and those later runs hand the
/// window already open; either tells of the ones ignored.</summary>
public static class RunArgumentsActions
{
    /// <summary>Decompiles to the language <paramref name="arguments"/> name, and starts with the assembly list they
    /// name, emptied first when they ask for that.</summary>
    public static void ApplyStartupOptions(AppState appState, RunArguments arguments)
    {
        ReportIgnored(appState, arguments);
        if (arguments.Language is not null)
        {
            SetLanguage(appState, arguments.Language.Value);
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

    /// <summary>Does what the arguments a later run handed over ask of the open window: decompiles
    /// to their language, switches to their list, added when there is none, clears the list, then opens
    /// their files and shows what they name.</summary>
    public static void ApplyArguments(AppState appState, RunArguments arguments)
    {
        ReportIgnored(appState, arguments);
        if (arguments.Language is not null)
        {
            SetLanguage(appState, arguments.Language.Value);
        }

        if (arguments.AssemblyList is not null)
        {
            OpenAssemblyListNamed(appState, arguments.AssemblyList);
        }

        if (arguments.ClearList)
        {
            CloseAllAssemblies(appState);
        }

        OpenRequestedFiles(appState, arguments.Files);
    }

    private static void ReportIgnored(AppState appState, RunArguments arguments)
    {
        if (arguments.Ignored.Count > 0)
        {
            Errors.Report(appState, new AppProblem.ArgumentsIgnored(arguments.Ignored.ToList()));
        }
    }

    /// <summary>Opens <paramref name="files"/>, then shows the namespace, type or member they
    /// ask to navigate to, or else the last file opened.</summary>
    public static void OpenRequestedFiles(AppState appState, FilesToOpen files)
    {
        if (files.NavigateTo is null)
        {
            OpenAssemblies(appState, files.Paths);
            return;
        }
        OpenAndShowId(appState, files.Paths, files.NavigateTo);
    }
}
