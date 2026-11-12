using System.Diagnostics;
using ILSight.Core;
using ILSight.Commanding;
using static ILSight.Analysis;
using static ILSight.AssemblyHandling.Assemblies;
using static ILSight.SavedDocuments;

namespace ILSight.AssemblyListKeeping;

/// <summary>Switching between the named assembly lists, and creating, renaming and deleting them.</summary>
public static class AssemblyLists
{
    /// <summary>Opens the open list's assemblies as the app starts, as a reload, so that the list is
    /// saved whole until they are all open, then shows what the tabs reopened from its documents showed.
    /// Files that are gone or fail to load stay listed as failed. Returns the loads that started in the tabs, for the
    /// edge that started this to start.</summary>
    public static Task<TabLoads> RestoreActiveAssemblyListAsync(AppState appState, CancellationToken ct)
    {
        var opening = BeginRestoringActiveAssemblyList(appState);
        return appState.OpenAssembliesAsReloadHandler.HandleAsync(appState, opening.Paths, opening.Reload, ct);
    }

    /// <summary>Uses what the open list picked for its assemblies and begins reopening them as a reload, the app saying
    /// it is reloading. Returns the files to open and the reload.</summary>
    private static (IReadOnlyList<AssemblyPath> Paths, ReloadTicket Reload) BeginRestoringActiveAssemblyList(AppState appState)
    {
        appState.Assemblies.Pdbs.Use(appState.Lists.ActivePdbs);
        appState.Assemblies.TargetFrameworks.Use(appState.Lists.ActiveFrameworks);
        var paths = appState.Lists.ActivePaths.ToList();
        return (paths, appState.Reload.Start());
    }

    /// <summary>Opens the list named <paramref name="name"/> as the app starts, in place of the one the
    /// settings had open, with the analyses and documents it had. A list by that name is added when there is none.</summary>
    public static void StartWithAssemblyList(AppState appState, string name)
    {
        if (!appState.Lists.HasList(name))
        {
            appState.Lists.Add(name);
        }

        if (!appState.Lists.TryActivate(name))
        {
            return;
        }

        appState.Analyzer.ClearRoots();
        appState.Analyzer.AddPending(appState.Lists.ActiveAnalyses);
        ReopenDocuments(appState);
    }

    /// <summary>Takes every assembly, and what was picked for it, off the open list before the app starts opening it.</summary>
    public static void EmptyActiveAssemblyList(AppState appState)
    {
        appState.Lists.RememberActivePaths([]);
        appState.Lists.RememberActivePdbs(new Dictionary<AssemblyPath, string>());
        appState.Lists.RememberActiveFrameworks(new Dictionary<AssemblyPath, TargetFrameworkId>());
    }

    /// <summary>Records the open assemblies and what was picked for them as the open list's, unless
    /// they are being reopened, when the list still holds them all.</summary>
    public static void RememberOpenAssemblies(AppState appState)
    {
        if (appState.IsReloading)
        {
            return;
        }

        appState.Lists.RememberActivePaths(AssemblyListing.ListFilePaths(appState.Assemblies));
        appState.Lists.RememberActivePdbs(appState.Assemblies.Pdbs.All);
        appState.Lists.RememberActiveFrameworks(appState.Assemblies.TargetFrameworks.All);
    }

    /// <summary>Records the open assemblies, the analyses and the documents as the open list's.</summary>
    internal static void RememberActiveAssemblyList(AppState appState)
    {
        RememberOpenAssemblies(appState);
        var analyzed = appState.Analyzer.ListSavedAnalyses();
        appState.Lists.RememberActiveAnalyses(analyzed);
        appState.Lists.RememberActiveDocuments(SaveDocuments(appState));
    }

    /// <summary>Opens the list named <paramref name="name"/> in place of the open one, with the
    /// analyses and documents it had, as <see cref="SwitchAssemblyList"/> then <see cref="OpenListAsync"/> do.
    /// Returns the loads that started in the tabs, and the finding of the list's analyses, for the edge that started
    /// this to start.</summary>
    public static Task<TabLoads> SwitchAssemblyListAsync(AppState appState, string name, CancellationToken ct)
    {
        return OpenWholeListAsync(appState, SwitchAssemblyList(appState, name), ct);
    }

    /// <summary>As <see cref="OpenListAsync"/>, for a workflow that hands the finding of the list's analyses back with
    /// the loads, rather than having the edge start it beside the opening.</summary>
    private static async Task<TabLoads> OpenWholeListAsync(AppState appState, ListOpening? opening, CancellationToken ct)
    {
        var loads = await OpenListAsync(appState, opening, ct);
        if (opening is null)
        {
            return loads;
        }

        return loads.With(opening.Analyses);
    }

    /// <summary>Makes the list named <paramref name="name"/> the open one, with the analyses and documents it had,
    /// without waiting for anything: the assemblies, documents, history and analyses of the one before go, kept with
    /// it for when it opens again; its documents reopen waiting for their places, its analyses wait to be found, and
    /// its assemblies begin reopening as a reload. Returns what is left for the edge to open
    /// (<see cref="OpenListAsync"/>); null when there is no list by that name.</summary>
    public static ListOpening? SwitchAssemblyList(AppState appState, string name)
    {
        RememberActiveAssemblyList(appState);
        if (!appState.Lists.TryActivate(name))
        {
            return null;
        }

        return BeginOpeningActiveList(appState);
    }

    /// <summary>Opens the list named <paramref name="name"/> in place of the open one, as
    /// <see cref="SwitchAssemblyListAsync"/> does, adding an empty one by that name when there is none.</summary>
    public static Task<TabLoads> OpenAssemblyListNamedAsync(AppState appState, string name, CancellationToken ct)
    {
        if (!appState.Lists.HasList(name))
        {
            appState.Lists.Add(name);
        }

        return SwitchAssemblyListAsync(appState, name, ct);
    }

    /// <summary>Puts the active list's documents and analyses in place of the open ones, each waiting, and begins
    /// reopening its assemblies in place of the open ones. Returns what is left to open.</summary>
    private static ListOpening BeginOpeningActiveList(AppState appState)
    {
        var closing = CloseAllAssemblies(appState);
        ReopenDocuments(appState);
        appState.History.Clear();
        var (paths, reload) = BeginRestoringActiveAssemblyList(appState);
        appState.Analyzer.AddPending(appState.Lists.ActiveAnalyses);
        var analyses = Analysis.BeginRestoring(appState.Analyzer, appState.Assemblies);
        return new ListOpening { Paths = paths, Reload = reload, Closing = closing, Analyses = analyses };
    }

    /// <summary>Opens the assemblies of the list <paramref name="opening"/> began opening, as the reload it holds;
    /// nothing when it is null. Finding the list's analyses is not this one's: the edge starts it beside this
    /// (<see cref="Commands.StartOpeningList"/>). Returns the loads that started in the tabs, those closing the list
    /// before it started first, for the edge that started this to start.</summary>
    public static async Task<TabLoads> OpenListAsync(AppState appState, ListOpening? opening, CancellationToken ct)
    {
        if (opening is null)
        {
            return TabLoads.None;
        }

        var restoring = appState.OpenAssembliesAsReloadHandler.HandleAsync(appState, opening.Paths, opening.Reload, ct);
        return opening.Closing.With(await restoring);
    }

    /// <summary>Adds an empty list named <paramref name="name"/>, or numbered when that is taken,
    /// leaving the open one open, and returns the name it got.</summary>
    public static string CreateAssemblyList(AppState appState, string name)
    {
        return appState.Lists.Add(name);
    }

    /// <summary>Adds a list of <paramref name="preset"/>'s assemblies under its name, leaving the open
    /// one open, and returns the name it got.</summary>
    public static string AddAssemblyListPreset(AppState appState, AssemblyListPreset preset)
    {
        return appState.Lists.Add(preset.Name, preset.Paths);
    }

    /// <summary>Adds a copy of the list named <paramref name="source"/> named <paramref name="name"/>, or
    /// numbered when that is taken, leaving the open one open, and returns the name the copy got.</summary>
    public static string DuplicateAssemblyList(AppState appState, string source, string name)
    {
        RememberActiveAssemblyList(appState);
        return appState.Lists.Duplicate(source, name);
    }

    /// <summary>Removes the list named <paramref name="name"/>. When it was open, the list that takes
    /// its place opens. Returns the loads that started in the tabs, for the edge that started this to start.</summary>
    public static Task<TabLoads> DeleteAssemblyListAsync(AppState appState, string name, CancellationToken ct)
    {
        return OpenWholeListAsync(appState, DeleteAssemblyList(appState, name), ct);
    }

    /// <summary>Removes the list named <paramref name="name"/>; when it was open, makes the list that takes its place
    /// the open one, as <see cref="SwitchAssemblyList"/> does. Returns what is left for the edge to open; null when it
    /// was not open.</summary>
    public static ListOpening? DeleteAssemblyList(AppState appState, string name)
    {
        var wasActive = string.Equals(name, appState.Lists.Active, StringComparison.OrdinalIgnoreCase);
        RememberActiveAssemblyList(appState);
        appState.Lists.Remove(name);
        if (!wasActive)
        {
            return null;
        }

        return BeginOpeningActiveList(appState);
    }

    /// <summary>Opens the dialog with the open list of <paramref name="lists"/> picked.</summary>
    public static void OpenListsDialog(AssemblyListsDialog dialog, SavedAssemblyLists lists)
    {
        dialog.Mode = new ListsDialogMode.Browsing();
        PickList(dialog, lists, lists.Active);
    }

    /// <summary>Picks the list named <paramref name="name"/> and starts renaming it.</summary>
    public static void PickToRename(AssemblyListsDialog dialog, SavedAssemblyLists lists, string name)
    {
        PickList(dialog, lists, name);
        StartRenaming(dialog, name);
    }

    /// <summary>Picks the list named <paramref name="name"/>, leaving any rename or deletion.</summary>
    public static void PickList(AssemblyListsDialog dialog, SavedAssemblyLists lists, string name)
    {
        var names = lists.Names;
        var index = FindListIndex(names, name);
        if (index is null)
        {
            dialog.Selection.Clear();
        }
        else
        {
            dialog.Selection.Select(index.Value, names.Count);
        }

        StopAsking(dialog);
    }

    /// <summary>The row of the list named <paramref name="name"/>, ignoring case and surrounding spaces; null when there is none.</summary>
    private static int? FindListIndex(IReadOnlyList<string> names, string name)
    {
        var trimmed = name.Trim();
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return null;
    }

    /// <summary>The name of the picked list, or null.</summary>
    public static string? FindPickedList(AssemblyListsDialog dialog, SavedAssemblyLists lists)
    {
        var names = lists.Names;
        var index = dialog.Selection.Index;
        if (index < 0)
        {
            return null;
        }

        if (index >= names.Count)
        {
            return null;
        }

        return names[index];
    }

    public static void StartRenaming(AssemblyListsDialog dialog, string name)
    {
        dialog.Mode = new ListsDialogMode.Renaming(name);
    }

    public static void StartConfirmingDelete(AssemblyListsDialog dialog, string name)
    {
        dialog.Mode = new ListsDialogMode.ConfirmingDelete(name);
    }

    /// <summary>Leaves the rename or deletion being asked about.</summary>
    public static void StopAsking(AssemblyListsDialog dialog)
    {
        if (!dialog.IsAsking)
        {
            return;
        }

        dialog.Mode = new ListsDialogMode.Browsing();
    }

    public static void CloseListsDialog(AssemblyListsDialog dialog)
    {
        dialog.Mode = new ListsDialogMode.Closed();
    }
}
