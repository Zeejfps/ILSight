using static ILSight.Assemblies;
using static ILSight.AssemblyLists;
using static ILSight.Navigation;
using static ILSight.Preferences;

namespace ILSight;

/// <summary>Doing what a command asks: the one the app starts with, and those later runs hand the
/// window already open; either tells of the arguments it ignored.</summary>
public static class ExternalCommands
{
    /// <summary>Decompiles to the language the command names, and starts with the assembly list it
    /// names, emptied first when it asks for that.</summary>
    public static void ApplyStartupOptions(AppState appState, ExternalCommand command)
    {
        ReportIgnored(appState, command);
        if (command.Language != null)
            SetLanguage(appState, command.Language.Value);
        if (command.AssemblyList != null)
            StartWithAssemblyList(appState, command.AssemblyList);
        if (command.ClearList)
            EmptyActiveAssemblyList(appState);
    }

    /// <summary>Does what a command a later run handed over asks of the open window: decompiles
    /// to its language, switches to its list, added when there is none, clears the list, then opens
    /// its files and shows what it names.</summary>
    public static void ApplyExternalCommand(AppState appState, ExternalCommand command)
    {
        ReportIgnored(appState, command);
        if (command.Language != null)
            SetLanguage(appState, command.Language.Value);
        if (command.AssemblyList != null)
            OpenAssemblyListNamed(appState, command.AssemblyList);
        if (command.ClearList)
            CloseAllAssemblies(appState);
        OpenCommandFiles(appState, command.Files);
    }

    private static void ReportIgnored(AppState appState, ExternalCommand command)
    {
        if (command.Ignored.Count > 0)
            appState.ReportError(new AppProblem.ArgumentsIgnored(command.Ignored.ToList()));
    }

    /// <summary>Opens the files a command names, then shows the namespace, type or member it
    /// asks to navigate to, or else the last file opened.</summary>
    public static void OpenCommandFiles(AppState appState, FilesToOpen files)
    {
        if (files.NavigateTo == null)
        {
            OpenAssemblies(appState, files.Paths);
            return;
        }
        OpenAndShowId(appState, files.Paths, files.NavigateTo);
    }
}
