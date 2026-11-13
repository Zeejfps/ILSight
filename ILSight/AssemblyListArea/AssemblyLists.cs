using System.Diagnostics;
using ILSight.Core;
using ILSight.CommandArea;

namespace ILSight.AssemblyListArea;

/// <summary>Switching between the named assembly lists, and creating, renaming and deleting them.</summary>
public static class AssemblyLists
{
    /// <summary>Records the open assemblies and what was picked for them as the open list's, unless
    /// they are being reopened, when the list still holds them all.</summary>
    public static void RememberOpenAssemblies(SavedAssemblyLists lists, OpenListSession session)
    {
        if (session.Reload.IsRunning)
        {
            return;
        }

        lists.RememberActivePaths(session.Assemblies.ListFilePaths());
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
        lists.RememberActiveDocuments(SavedDocuments.SaveDocuments(session, view));
    }

    /// <summary>Makes the list named <paramref name="name"/> the open one, with the analyses and documents it had,
    /// without waiting for anything: the assemblies, documents, history and analyses of the one before go, kept with
    /// it for when it opens again; its documents reopen waiting for their places, its analyses wait to be found, and
    /// its assemblies begin reopening as a reload. Returns what is left for the edge to open
    /// (<see cref="Commands.StartOpeningList"/>); null when there is no list by that name.</summary>
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

    private static ListOpening BeginOpeningActiveList(SavedAssemblyLists lists, OpenListSession session)
    {
        var closing = session.CloseAllAssemblies();
        SavedDocuments.ReopenDocuments(lists, session);
        session.Navigation.ClearHistory();
        var reopening = session.BeginReopening(lists);
        session.Analyzer.AddPending(lists.ActiveAnalyses);
        var analyses = Analysis.BeginRestoring(session.Analyzer, session.Assemblies);
        return new ListOpening { Reopening = reopening, Closing = closing, Analyses = analyses };
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
