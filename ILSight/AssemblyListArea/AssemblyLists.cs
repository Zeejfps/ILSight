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
}
