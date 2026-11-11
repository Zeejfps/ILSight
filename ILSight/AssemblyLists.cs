using System.Diagnostics;
using ILSight.Core;
using static ILSight.Analysis;
using static ILSight.Assemblies;
using static ILSight.SavedDocuments;

namespace ILSight;

/// <summary>Switching between the named assembly lists, and creating, renaming and deleting them.</summary>
public static class AssemblyLists
{
    /// <summary>Opens the open list's assemblies as the app starts, as a reload, so that the list is
    /// saved whole until they are all open, then shows what the tabs reopened from its documents showed.
    /// Files that are gone or fail to load stay listed as failed.</summary>
    public static void RestoreActiveAssemblyList(AppState appState)
    {
        appState.Assemblies.Pdbs.Use(appState.Lists.ActivePdbs);
        appState.Assemblies.TargetFrameworks.Use(appState.Lists.ActiveFrameworks);
        var paths = appState.Lists.ActivePaths.ToList();
        OpenAssembliesAsReload(appState, paths);
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
    /// analyses and documents it had. The assemblies, documents, history and analyses of the one before go, kept
    /// with it for when it opens again.</summary>
    public static void SwitchAssemblyList(AppState appState, string name)
    {
        RememberActiveAssemblyList(appState);
        if (!appState.Lists.TryActivate(name))
        {
            return;
        }

        OpenActiveList(appState);
    }

    /// <summary>Opens the list named <paramref name="name"/> in place of the open one, as
    /// <see cref="SwitchAssemblyList"/> does, adding an empty one by that name when there is none.</summary>
    public static void OpenAssemblyListNamed(AppState appState, string name)
    {
        if (!appState.Lists.HasList(name))
        {
            appState.Lists.Add(name);
        }

        SwitchAssemblyList(appState, name);
    }

    private static void OpenActiveList(AppState appState)
    {
        CloseAllAssemblies(appState);
        ReopenDocuments(appState);
        appState.History.Clear();
        RestoreActiveAssemblyList(appState);
        appState.Analyzer.AddPending(appState.Lists.ActiveAnalyses);
        RestoreAnalyses(appState);
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
    /// its place opens.</summary>
    public static void DeleteAssemblyList(AppState appState, string name)
    {
        var wasActive = string.Equals(name, appState.Lists.Active, StringComparison.OrdinalIgnoreCase);
        RememberActiveAssemblyList(appState);
        appState.Lists.Remove(name);
        if (wasActive)
        {
            OpenActiveList(appState);
        }
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
        var index = GetListIndex(names, name);
        dialog.Selection.Select(index, names.Count);
        StopAsking(dialog);
    }

    private static int GetListIndex(IReadOnlyList<string> names, string name)
    {
        var trimmed = name.Trim();
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
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
