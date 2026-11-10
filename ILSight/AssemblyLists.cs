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
        if (!AssemblyLists.HasList(appState.Lists, name))
            AssemblyLists.AddList(appState.Lists, name);
        if (!AssemblyLists.ActivateList(appState.Lists, name))
            return;
        Analysis.ClearRoots(appState.Analyzer);
        Analysis.AddPending(appState.Analyzer, appState.Lists.ActiveAnalyses);
        ReopenDocuments(appState);
    }

    /// <summary>Takes every assembly, and what was picked for it, off the open list before the app starts opening it.</summary>
    public static void EmptyActiveAssemblyList(AppState appState)
    {
        AssemblyLists.RememberActive(appState.Lists, []);
        AssemblyLists.RememberActivePdbs(appState.Lists, new Dictionary<AssemblyPath, string>());
        AssemblyLists.RememberActiveFrameworks(appState.Lists, new Dictionary<AssemblyPath, TargetFrameworkId>());
    }

    /// <summary>Records the open assemblies and what was picked for them as the open list's, unless
    /// they are being reopened, when the list still holds them all.</summary>
    public static void RememberOpenAssemblies(AppState appState)
    {
        if (appState.IsReloading)
            return;
        AssemblyLists.RememberActive(appState.Lists, AssemblyListing.ListFilePaths(appState.Assemblies));
        AssemblyLists.RememberActivePdbs(appState.Lists, appState.Assemblies.Pdbs.All);
        AssemblyLists.RememberActiveFrameworks(appState.Lists, appState.Assemblies.TargetFrameworks.All);
    }

    /// <summary>Records the open assemblies, the analyses and the documents as the open list's.</summary>
    internal static void RememberActiveAssemblyList(AppState appState)
    {
        RememberOpenAssemblies(appState);
        var analyzed = Analysis.ListSavedAnalyses(appState.Analyzer);
        AssemblyLists.RememberActiveAnalyses(appState.Lists, analyzed);
        AssemblyLists.RememberActiveDocuments(appState.Lists, SaveDocuments(appState));
    }

    /// <summary>Opens the list named <paramref name="name"/> in place of the open one, with the
    /// analyses and documents it had. The assemblies, documents, history and analyses of the one before go, kept
    /// with it for when it opens again.</summary>
    public static void SwitchAssemblyList(AppState appState, string name)
    {
        RememberActiveAssemblyList(appState);
        if (!AssemblyLists.ActivateList(appState.Lists, name))
            return;
        OpenActiveList(appState);
    }

    /// <summary>Opens the list named <paramref name="name"/> in place of the open one, as
    /// <see cref="SwitchAssemblyList"/> does, adding an empty one by that name when there is none.</summary>
    public static void OpenAssemblyListNamed(AppState appState, string name)
    {
        if (!AssemblyLists.HasList(appState.Lists, name))
            AssemblyLists.AddList(appState.Lists, name);
        SwitchAssemblyList(appState, name);
    }

    private static void OpenActiveList(AppState appState)
    {
        CloseAllAssemblies(appState);
        ReopenDocuments(appState);
        Navigation.ClearHistory(appState.History);
        RestoreActiveAssemblyList(appState);
        Analysis.AddPending(appState.Analyzer, appState.Lists.ActiveAnalyses);
        RestoreAnalyses(appState);
    }

    /// <summary>Adds an empty list named <paramref name="name"/>, or numbered when that is taken,
    /// leaving the open one open, and returns the name it got.</summary>
    public static string CreateAssemblyList(AppState appState, string name)
    {
        return AssemblyLists.AddList(appState.Lists, name);
    }

    /// <summary>Adds a list of <paramref name="preset"/>'s assemblies under its name, leaving the open
    /// one open, and returns the name it got.</summary>
    public static string AddAssemblyListPreset(AppState appState, AssemblyListPreset preset)
    {
        return AssemblyLists.AddList(appState.Lists, preset.Name, preset.Paths);
    }

    /// <summary>Adds a copy of the list named <paramref name="source"/> named <paramref name="name"/>, or
    /// numbered when that is taken, leaving the open one open, and returns the name the copy got.</summary>
    public static string DuplicateAssemblyList(AppState appState, string source, string name)
    {
        RememberActiveAssemblyList(appState);
        return AssemblyLists.DuplicateList(appState.Lists, source, name);
    }

    /// <summary>Removes the list named <paramref name="name"/>. When it was open, the list that takes
    /// its place opens.</summary>
    public static void DeleteAssemblyList(AppState appState, string name)
    {
        var wasActive = string.Equals(name, appState.Lists.Active, StringComparison.OrdinalIgnoreCase);
        RememberActiveAssemblyList(appState);
        AssemblyLists.DeleteList(appState.Lists, name);
        if (wasActive)
            OpenActiveList(appState);
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
        dialog.Selection.Select(index, names.Count);
        StopAsking(dialog);
    }

    private static int FindListIndex(IReadOnlyList<string> names, string name)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], name.Trim(), StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>The name of the picked list, or null.</summary>
    public static string? FindPickedList(AssemblyListsDialog dialog, SavedAssemblyLists lists)
    {
        var names = lists.Names;
        var index = dialog.Selection.Index;
        if (index < 0)
            return null;
        if (index >= names.Count)
            return null;
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
            return;
        dialog.Mode = new ListsDialogMode.Browsing();
    }

    public static void CloseListsDialog(AssemblyListsDialog dialog)
    {
        dialog.Mode = new ListsDialogMode.Closed();
    }

    /// <summary>Takes the lists the settings kept, dropping nameless ones and repeated names, and
    /// opens the one named <paramref name="active"/>, else the first.</summary>
    public static void LoadLists(SavedAssemblyLists lists, List<AssemblyListRecord> records, string? active)
    {
        lists.Lists.Clear();
        foreach (var record in records)
            LoadList(lists, record);
        if (lists.Lists.Count == 0)
            lists.Lists.Add(CreateEmpty(SavedAssemblyLists.DefaultName));
        lists.ActiveList = FindList(lists, active) ?? lists.Lists[0];
        lists.Revision++;
    }

    private static void LoadList(SavedAssemblyLists lists, AssemblyListRecord record)
    {
        var name = record.Name.Trim();
        if (name.Length == 0)
            return;
        if (FindList(lists, name) != null)
            return;
        var analyzed = record.Analyzed ?? new List<CodeAnchor>();
        var list = new SavedAssemblyLists.SavedList { Name = name, Paths = record.Assemblies.ToList(), Analyzed = analyzed.ToList() };
        CopyPicks(record.Pdbs, list.Pdbs);
        ReadFrameworks(record.Frameworks, list.Frameworks);
        list.Documents = record.Documents;
        lists.Lists.Add(list);
    }

    /// <summary>The lists there are before the settings are read: an empty one named <see cref="SavedAssemblyLists.DefaultName"/>,
    /// which is open.</summary>
    public static SavedAssemblyLists CreateLists()
    {
        var active = CreateEmpty(SavedAssemblyLists.DefaultName);
        return new SavedAssemblyLists { ActiveList = active, Lists = [active] };
    }

    private static SavedAssemblyLists.SavedList CreateEmpty(string name)
    {
        return new SavedAssemblyLists.SavedList { Name = name, Paths = new List<AssemblyPath>(), Analyzed = new List<CodeAnchor>() };
    }

    private static void CopyPicks<T>(IReadOnlyDictionary<AssemblyPath, T>? picks, Dictionary<AssemblyPath, T> into)
    {
        if (picks == null)
            return;
        foreach (var (assembly, pick) in picks)
            into[assembly] = pick;
    }

    /// <summary>Puts the frameworks the settings wrote as <paramref name="saved"/> into <paramref name="into"/>,
    /// leaving out one that is not a framework id.</summary>
    private static void ReadFrameworks(IReadOnlyDictionary<AssemblyPath, string>? saved, Dictionary<AssemblyPath, TargetFrameworkId> into)
    {
        if (saved == null)
            return;
        foreach (var (assembly, text) in saved)
        {
            var framework = TargetFrameworkId.Parse(text);
            if (framework != null)
                into[assembly] = framework;
        }
    }

    public static List<AssemblyListRecord> SaveLists(SavedAssemblyLists lists)
    {
        return lists.Lists
            .Select(static l => new AssemblyListRecord(l.Name, l.Paths.ToList(), l.Analyzed.ToList(), SavePicks(l.Pdbs), SaveFrameworks(l.Frameworks), l.Documents))
            .ToList();
    }

    /// <summary>The panels the documents kept with every list were docked as.</summary>
    public static List<string> ListDocumentPanels(SavedAssemblyLists lists)
    {
        return lists.Lists
            .Select(static l => l.Documents)
            .OfType<DocumentsRecord>()
            .SelectMany(static d => d.Tabs)
            .Select(static t => t.Panel)
            .ToList();
    }

    /// <summary>A list's picks as the settings keep them; null for none, which leaves them out.</summary>
    private static Dictionary<AssemblyPath, string>? SavePicks(Dictionary<AssemblyPath, string> picks)
    {
        if (picks.Count == 0)
            return null;
        return new Dictionary<AssemblyPath, string>(picks);
    }

    /// <summary>A list's frameworks as the settings keep them, as written; null for none.</summary>
    private static Dictionary<AssemblyPath, string>? SaveFrameworks(Dictionary<AssemblyPath, TargetFrameworkId> frameworks)
    {
        if (frameworks.Count == 0)
            return null;
        return frameworks.ToDictionary(static f => f.Key, static f => f.Value.Text);
    }

    public static bool HasList(SavedAssemblyLists lists, string name)
    {
        return FindList(lists, name) != null;
    }

    /// <summary>The file paths of the assemblies in the list named <paramref name="name"/>; none when there is no such list.</summary>
    public static IReadOnlyList<AssemblyPath> GetPaths(SavedAssemblyLists lists, string name)
    {
        var list = FindList(lists, name);
        if (list == null)
            return [];
        return list.Paths;
    }

    /// <summary>Opens the list named <paramref name="name"/>. False when it is the open one already or there is none.</summary>
    public static bool ActivateList(SavedAssemblyLists lists, string name)
    {
        var list = FindList(lists, name);
        if (list == null)
            return false;
        if (list == lists.ActiveList)
            return false;
        lists.ActiveList = list;
        lists.Revision++;
        return true;
    }

    /// <summary>Records <paramref name="paths"/> as the open list's assemblies.</summary>
    public static void RememberActive(SavedAssemblyLists lists, IReadOnlyList<AssemblyPath> paths)
    {
        if (lists.ActiveList.Paths.SequenceEqual(paths))
            return;
        lists.ActiveList.Paths.Clear();
        lists.ActiveList.Paths.AddRange(paths);
        lists.Revision++;
    }

    /// <summary>Records <paramref name="analyzed"/> as the symbols analyzed in the open list.</summary>
    public static void RememberActiveAnalyses(SavedAssemblyLists lists, IReadOnlyList<CodeAnchor> analyzed)
    {
        if (lists.ActiveList.Analyzed.SequenceEqual(analyzed))
            return;
        lists.ActiveList.Analyzed.Clear();
        lists.ActiveList.Analyzed.AddRange(analyzed);
        lists.Revision++;
    }

    /// <summary>Keeps <paramref name="documents"/>, or none when null, as the documents the open list has open.</summary>
    public static void RememberActiveDocuments(SavedAssemblyLists lists, DocumentsRecord? documents)
    {
        lists.ActiveList.Documents = documents;
    }

    /// <summary>Records <paramref name="pdbs"/> as the PDBs picked for the open list's assemblies.</summary>
    public static void RememberActivePdbs(SavedAssemblyLists lists, IReadOnlyDictionary<AssemblyPath, string> pdbs)
    {
        RememberPicks(lists, lists.ActiveList.Pdbs, pdbs);
    }

    /// <summary>Records <paramref name="frameworks"/> as the frameworks picked for the open list's assemblies.</summary>
    public static void RememberActiveFrameworks(SavedAssemblyLists lists, IReadOnlyDictionary<AssemblyPath, TargetFrameworkId> frameworks)
    {
        RememberPicks(lists, lists.ActiveList.Frameworks, frameworks);
    }

    private static void RememberPicks<T>(SavedAssemblyLists lists, Dictionary<AssemblyPath, T> kept, IReadOnlyDictionary<AssemblyPath, T> picks) where T : class
    {
        if (HasPicks(kept, picks))
            return;
        kept.Clear();
        CopyPicks(picks, kept);
        lists.Revision++;
    }

    private static bool HasPicks<T>(Dictionary<AssemblyPath, T> kept, IReadOnlyDictionary<AssemblyPath, T> picks) where T : class
    {
        if (kept.Count != picks.Count)
            return false;
        return picks.All(p => Equals(kept.GetValueOrDefault(p.Key), p.Value));
    }

    /// <summary>Adds an empty list named <paramref name="name"/>, numbered when that name is taken,
    /// and returns the name it got.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public static string AddList(SavedAssemblyLists lists, string name)
    {
        return InsertList(lists, new SavedAssemblyLists.SavedList { Name = name, Paths = new List<AssemblyPath>(), Analyzed = new List<CodeAnchor>() });
    }

    /// <summary>Adds a list of the assemblies at <paramref name="paths"/>, named <paramref name="name"/>
    /// or numbered when that is taken, and returns the name it got.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public static string AddList(SavedAssemblyLists lists, string name, IReadOnlyList<AssemblyPath> paths)
    {
        return InsertList(lists, new SavedAssemblyLists.SavedList { Name = name, Paths = paths.ToList(), Analyzed = new List<CodeAnchor>() });
    }

    /// <summary>Adds a list with the assemblies, analyses and picks of the one named
    /// <paramref name="source"/>, named <paramref name="name"/> or numbered when that is taken, and
    /// returns the name it got.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public static string DuplicateList(SavedAssemblyLists lists, string source, string name)
    {
        var copy = new SavedAssemblyLists.SavedList { Name = name, Paths = GetPaths(lists, source).ToList(), Analyzed = GetAnalyses(lists, source).ToList() };
        var original = FindList(lists, source);
        if (original != null)
            CopyPicksOf(original, copy);
        return InsertList(lists, copy);
    }

    private static void CopyPicksOf(SavedAssemblyLists.SavedList original, SavedAssemblyLists.SavedList copy)
    {
        CopyPicks(original.Pdbs, copy.Pdbs);
        CopyPicks(original.Frameworks, copy.Frameworks);
    }

    private static IReadOnlyList<CodeAnchor> GetAnalyses(SavedAssemblyLists lists, string name)
    {
        var list = FindList(lists, name);
        if (list == null)
            return [];
        return list.Analyzed;
    }

    /// <summary>Adds <paramref name="list"/>, numbered when its name is taken, and returns the name it got.</summary>
    private static string InsertList(SavedAssemblyLists lists, SavedAssemblyLists.SavedList list)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(list.Name);
        list.Name = MakeUniqueName(lists, list.Name.Trim());
        lists.Lists.Add(list);
        lists.Revision++;
        return list.Name;
    }

    private static string MakeUniqueName(SavedAssemblyLists lists, string name)
    {
        if (FindList(lists, name) == null)
            return name;
        var number = 2;
        while (FindList(lists, $"{name} {number}") != null)
            number++;
        return $"{name} {number}";
    }

    /// <summary>Whether the list named <paramref name="name"/> may be renamed to <paramref name="newName"/>:
    /// it is not empty and no other list has it.</summary>
    public static bool CanRenameList(SavedAssemblyLists lists, string name, string newName)
    {
        var trimmed = newName.Trim();
        if (trimmed.Length == 0)
            return false;
        var holder = FindList(lists, trimmed);
        if (holder == null)
            return true;
        return holder == FindList(lists, name);
    }

    /// <summary>Renames the list named <paramref name="name"/>, unless <see cref="CanRenameList"/> says no.</summary>
    public static bool RenameList(SavedAssemblyLists lists, string name, string newName)
    {
        if (!CanRenameList(lists, name, newName))
            return false;
        var list = FindList(lists, name);
        if (list == null)
            return false;
        list.Name = newName.Trim();
        lists.Revision++;
        return true;
    }

    /// <summary>Removes the list named <paramref name="name"/>. When it was the open one, the list
    /// that took its place opens, or the one before it when it was last.</summary>
    public static void DeleteList(SavedAssemblyLists lists, string name)
    {
        var list = FindList(lists, name);
        if (list == null)
            return;
        var replacement = FindReplacement(lists, list.Name);
        lists.Lists.Remove(list);
        if (lists.Lists.Count == 0)
            lists.Lists.Add(CreateEmpty(SavedAssemblyLists.DefaultName));
        if (list == lists.ActiveList)
            lists.ActiveList = FindRemainingList(lists, replacement);
        lists.Revision++;
    }

    /// <summary>The name of the list that opens when the open list named <paramref name="name"/> is
    /// deleted: the one after it, else the one before, else a new empty <see cref="SavedAssemblyLists.DefaultName"/>.</summary>
    public static string FindReplacement(SavedAssemblyLists lists, string name)
    {
        var list = FindList(lists, name);
        if (list == null)
            return lists.Active;
        var index = lists.Lists.IndexOf(list);
        if (index + 1 < lists.Lists.Count)
            return lists.Lists[index + 1].Name;
        if (index > 0)
            return lists.Lists[index - 1].Name;
        return SavedAssemblyLists.DefaultName;
    }

    /// <summary>The list named <paramref name="name"/>, which <see cref="FindReplacement"/> named after the
    /// list it replaces was removed: one of those left, or the empty one made when none was.</summary>
    private static SavedAssemblyLists.SavedList FindRemainingList(SavedAssemblyLists lists, string name)
    {
        var list = FindList(lists, name);
        if (list == null)
            throw new UnreachableException($"No list named {name} is left, though the list that takes a deleted one's place always is.");
        return list;
    }

    private static SavedAssemblyLists.SavedList? FindList(SavedAssemblyLists lists, string? name)
    {
        if (name == null)
            return null;
        return lists.Lists.Find(l => string.Equals(l.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
