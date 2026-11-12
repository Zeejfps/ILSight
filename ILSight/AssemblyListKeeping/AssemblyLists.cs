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
        var opening = BeginRestoringActiveAssemblyList(appState.Lists, appState.ListSession);
        return appState.OpenAssembliesAsReloadHandler.HandleAsync(appState, opening.Paths, opening.Reload, ct);
    }

    /// <summary>Uses what the open list picked for its assemblies and begins reopening them as a reload, the app saying
    /// it is reloading. Returns the files to open and the reload.</summary>
    private static (IReadOnlyList<AssemblyPath> Paths, ReloadTicket Reload) BeginRestoringActiveAssemblyList(SavedAssemblyLists lists,
        OpenListSession session)
    {
        session.Assemblies.Pdbs.Use(lists.ActivePdbs);
        session.Assemblies.TargetFrameworks.Use(lists.ActiveFrameworks);
        var paths = lists.ActivePaths.ToList();
        return (paths, session.Reload.Start());
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
        ReopenDocuments(appState.Lists, appState.ListSession);
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
    public static void RememberOpenAssemblies(SavedAssemblyLists lists, OpenListSession session)
    {
        if (session.Reload.IsRunning)
        {
            return;
        }

        lists.RememberActivePaths(AssemblyListing.ListFilePaths(session.Assemblies));
        lists.RememberActivePdbs(session.Assemblies.Pdbs.All);
        lists.RememberActiveFrameworks(session.Assemblies.TargetFrameworks.All);
    }

    /// <summary>Records the open assemblies, the analyses and the documents, docked as <paramref name="view"/> says, as
    /// the open list's.</summary>
    internal static void RememberActiveAssemblyList(SavedAssemblyLists lists, OpenListSession session, ViewSettings view)
    {
        RememberOpenAssemblies(lists, session);
        var analyzed = session.Analyzer.ListSavedAnalyses();
        lists.RememberActiveAnalyses(analyzed);
        lists.RememberActiveDocuments(SaveDocuments(session, view));
    }

    /// <summary>Opens the list named <paramref name="name"/> in place of the open one, with the
    /// analyses and documents it had, as <see cref="SwitchAssemblyList"/> then <see cref="OpenListAsync"/> do.
    /// Returns the loads that started in the tabs, and the finding of the list's analyses, for the edge that started
    /// this to start.</summary>
    public static Task<TabLoads> SwitchAssemblyListAsync(AppState appState, string name, CancellationToken ct)
    {
        return OpenWholeListAsync(appState, SwitchAssemblyList(appState.Lists, appState.ListSession, appState.View, name), ct);
    }

    /// <summary>As <see cref="OpenListAsync"/>, for a workflow that hands back the loads closing the list before left and
    /// the finding of the list's analyses with the opening's loads, rather than having the edge start them beside the
    /// opening.</summary>
    private static async Task<TabLoads> OpenWholeListAsync(AppState appState, ListOpening? opening, CancellationToken ct)
    {
        var loads = await OpenListAsync(appState, opening, ct);
        if (opening is null)
        {
            return loads;
        }

        return opening.Closing.With(loads).With(opening.Analyses);
    }

    /// <summary>Makes the list named <paramref name="name"/> the open one, with the analyses and documents it had,
    /// without waiting for anything: the assemblies, documents, history and analyses of the one before go, kept with
    /// it for when it opens again; its documents reopen waiting for their places, its analyses wait to be found, and
    /// its assemblies begin reopening as a reload. Returns what is left for the edge to open
    /// (<see cref="OpenListAsync"/>); null when there is no list by that name.</summary>
    public static ListOpening? SwitchAssemblyList(SavedAssemblyLists lists, OpenListSession session, ViewSettings view, string name)
    {
        RememberActiveAssemblyList(lists, session, view);
        if (!lists.TryActivate(name))
        {
            return null;
        }

        return BeginOpeningActiveList(lists, session);
    }

    /// <summary>Makes the list named <paramref name="name"/> the open one, as <see cref="SwitchAssemblyList"/> does,
    /// adding an empty one by that name when there is none. Returns what is left for the edge to open.</summary>
    public static ListOpening? SwitchToListNamed(SavedAssemblyLists lists, OpenListSession session, ViewSettings view, string name)
    {
        if (!lists.HasList(name))
        {
            lists.Add(name);
        }

        return SwitchAssemblyList(lists, session, view, name);
    }

    /// <summary>Puts the active list's documents and analyses in place of the open ones, each waiting, and begins
    /// reopening its assemblies in place of the open ones. Returns what is left to open.</summary>
    private static ListOpening BeginOpeningActiveList(SavedAssemblyLists lists, OpenListSession session)
    {
        var closing = CloseAllAssemblies(session);
        ReopenDocuments(lists, session);
        session.Navigation.ClearHistory();
        var (paths, reload) = BeginRestoringActiveAssemblyList(lists, session);
        session.Analyzer.AddPending(lists.ActiveAnalyses);
        var analyses = Analysis.BeginRestoring(session.Analyzer, session.Assemblies);
        return new ListOpening { Paths = paths, Reload = reload, Closing = closing, Analyses = analyses };
    }

    /// <summary>Opens the assemblies of the list <paramref name="opening"/> began opening, as the reload it holds;
    /// nothing when it is null. Finding the list's analyses is not this one's: the edge starts it beside this
    /// (<see cref="Commands.StartOpeningList"/>), and nor are the loads closing the list before it left: the edge
    /// starts them before this, which they need not wait for. Returns the loads that started in the tabs, for the edge
    /// that started this to start.</summary>
    public static async Task<TabLoads> OpenListAsync(AppState appState, ListOpening? opening, CancellationToken ct)
    {
        if (opening is null)
        {
            return TabLoads.None;
        }

        return await appState.OpenAssembliesAsReloadHandler.HandleAsync(appState, opening.Paths, opening.Reload, ct);
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
        RememberActiveAssemblyList(appState.Lists, appState.ListSession, appState.View);
        return appState.Lists.Duplicate(source, name);
    }

    /// <summary>Removes the list named <paramref name="name"/>. When it was open, the list that takes
    /// its place opens. Returns the loads that started in the tabs, for the edge that started this to start.</summary>
    public static Task<TabLoads> DeleteAssemblyListAsync(AppState appState, string name, CancellationToken ct)
    {
        return OpenWholeListAsync(appState, DeleteAssemblyList(appState.Lists, appState.ListSession, appState.View, name), ct);
    }

    /// <summary>Removes the list named <paramref name="name"/>; when it was open, makes the list that takes its place
    /// the open one, as <see cref="SwitchAssemblyList"/> does. Returns what is left for the edge to open; null when it
    /// was not open.</summary>
    public static ListOpening? DeleteAssemblyList(SavedAssemblyLists lists, OpenListSession session, ViewSettings view, string name)
    {
        var wasActive = string.Equals(name, lists.Active, StringComparison.OrdinalIgnoreCase);
        RememberActiveAssemblyList(lists, session, view);
        lists.Remove(name);
        if (!wasActive)
        {
            return null;
        }

        return BeginOpeningActiveList(lists, session);
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
