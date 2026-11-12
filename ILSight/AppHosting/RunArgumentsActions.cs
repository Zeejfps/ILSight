using static ILSight.AssemblyHandling.Assemblies;
using static ILSight.AssemblyListKeeping.AssemblyLists;

namespace ILSight.AppHosting;

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
            appState.Language = arguments.Language.Value;
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
    /// their files and shows what they name. The list's assemblies open while the files do. Returns the loads that
    /// started in the tabs, the one showing what the files name among them, for the edge to start.</summary>
    public static async Task<TabLoads> ApplyArgumentsAsync(AppState appState, RunArguments arguments, CancellationToken ct)
    {
        if (arguments.Language is not null)
        {
            appState.Language = arguments.Language.Value;
        }

        var switching = Task.FromResult(TabLoads.None);
        if (arguments.AssemblyList is not null)
        {
            switching = OpenAssemblyListNamedAsync(appState, arguments.AssemblyList, ct);
        }

        var clearing = TabLoads.None;
        if (arguments.ClearList)
        {
            clearing = CloseAllAssemblies(appState);
        }

        var opening = OpenRequestedFilesAsync(appState, arguments.Files, ct);
        await Task.WhenAll(switching, opening);
        return (await switching).With(clearing).With(await opening);
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
