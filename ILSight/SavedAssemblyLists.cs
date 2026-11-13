using System.Diagnostics;
using ILSight.Core;
using ILSight.AssemblyListArea;

namespace ILSight;

/// <summary>
/// The user's named assembly lists and which one is open. Names are never empty and never the same
/// twice, ignoring case, and there is always a list open: deleting the last one leaves an empty one
/// named <see cref="DefaultName"/>. Its methods are the only way to change the lists, and keep all of that.
/// </summary>
public sealed class SavedAssemblyLists
{
    public const string DefaultName = "Default";

    private readonly List<SavedList> _lists;
    private SavedList _active;

    /// <summary>The lists there are before the settings are read: an empty one named <see cref="DefaultName"/>,
    /// which is open.</summary>
    public SavedAssemblyLists()
    {
        _active = CreateEmpty(DefaultName);
        _lists = [_active];
    }

    /// <summary>Changes whenever a list is added, renamed, removed or opened, or its assemblies or analyses
    /// change; not when its documents do, which the open tabs count.</summary>
    public int Revision { get; private set; }

    /// <summary>The name of the list that is open.</summary>
    public string Active => _active.Name;

    /// <summary>The file paths of the open list's assemblies.</summary>
    public IReadOnlyList<AssemblyPath> ActivePaths => _active.Paths;

    /// <summary>The symbols analyzed in the open list.</summary>
    public IReadOnlyList<CodeAnchor> ActiveAnalyses => _active.Analyzed;

    /// <summary>The PDBs picked for the open list's assemblies, by assembly path.</summary>
    public IReadOnlyDictionary<AssemblyPath, string> ActivePdbs => _active.Pdbs;

    /// <summary>The frameworks picked for the open list's assemblies, by assembly path.</summary>
    public IReadOnlyDictionary<AssemblyPath, TargetFrameworkId> ActiveFrameworks => _active.Frameworks;

    /// <summary>The documents the open list had open when they were last kept with it, or null for none.</summary>
    public DocumentsRecord? ActiveDocuments => _active.Documents;

    public IReadOnlyList<string> Names => _lists.ConvertAll(static l => l.Name);

    public int Count => _lists.Count;

    private sealed class SavedList
    {
        public required string Name { get; set; }
        public DocumentsRecord? Documents { get; set; }
        public required List<AssemblyPath> Paths { get; init; }
        public required List<CodeAnchor> Analyzed { get; init; }
        public Dictionary<AssemblyPath, string> Pdbs { get; } = new();
        public Dictionary<AssemblyPath, TargetFrameworkId> Frameworks { get; } = new();
    }

    /// <summary>Takes the lists the settings kept, dropping nameless ones and repeated names, and
    /// opens the one named <paramref name="active"/>, else the first.</summary>
    public void Restore(IEnumerable<AssemblyListRecord> records, string? active)
    {
        _lists.Clear();
        foreach (var record in records)
        {
            RestoreList(record);
        }

        if (_lists.Count == 0)
        {
            _lists.Add(CreateEmpty(DefaultName));
        }

        _active = FindList(active) ?? _lists[0];
        NoteChange();
    }

    private void RestoreList(AssemblyListRecord record)
    {
        var name = record.Name.Trim();
        if (name.Length == 0)
        {
            return;
        }

        var existing = FindList(name);
        if (existing is not null)
        {
            return;
        }

        var analyzed = record.Analyzed ?? new List<CodeAnchor>();
        var list = new SavedList { Name = name, Paths = record.Assemblies.ToList(), Analyzed = analyzed.ToList() };
        CopyPicks(record.Pdbs, list.Pdbs);
        ReadFrameworks(record.Frameworks, list.Frameworks);
        list.Documents = record.Documents;
        _lists.Add(list);
    }

    private static SavedList CreateEmpty(string name)
    {
        return new SavedList { Name = name, Paths = new List<AssemblyPath>(), Analyzed = new List<CodeAnchor>() };
    }

    private static void CopyPicks<T>(IReadOnlyDictionary<AssemblyPath, T>? picks, Dictionary<AssemblyPath, T> into)
    {
        if (picks is null)
        {
            return;
        }

        foreach (var (assembly, pick) in picks)
        {
            into[assembly] = pick;
        }
    }

    private static void ReadFrameworks(IReadOnlyDictionary<AssemblyPath, string>? saved, Dictionary<AssemblyPath, TargetFrameworkId> into)
    {
        if (saved is null)
        {
            return;
        }

        foreach (var (assembly, text) in saved)
        {
            var framework = TargetFrameworkId.Parse(text);
            if (framework != null)
            {
                into[assembly] = framework;
            }
        }
    }

    /// <summary>The lists as the settings keep them.</summary>
    public List<AssemblyListRecord> ToRecords()
    {
        return _lists
            .Select(static l => new AssemblyListRecord
            {
                Name = l.Name,
                Assemblies = l.Paths.ToList(),
                Analyzed = l.Analyzed.ToList(),
                Pdbs = SavePicks(l.Pdbs),
                Frameworks = SaveFrameworks(l.Frameworks),
                Documents = l.Documents,
            })
            .ToList();
    }

    /// <summary>The panels the documents kept with every list were docked as.</summary>
    public List<string> ListDocumentPanels()
    {
        return _lists
            .Select(static l => l.Documents)
            .OfType<DocumentsRecord>()
            .SelectMany(static d => d.Tabs)
            .Select(static t => t.Panel)
            .ToList();
    }

    private static Dictionary<AssemblyPath, string>? SavePicks(Dictionary<AssemblyPath, string> picks)
    {
        // Null, not empty, leaves them out of the settings.
        if (picks.Count == 0)
        {
            return null;
        }

        return new Dictionary<AssemblyPath, string>(picks);
    }

    private static Dictionary<AssemblyPath, string>? SaveFrameworks(Dictionary<AssemblyPath, TargetFrameworkId> frameworks)
    {
        if (frameworks.Count == 0)
        {
            return null;
        }

        return frameworks.ToDictionary(static f => f.Key, static f => f.Value.Text);
    }

    public bool HasList(string name)
    {
        return FindList(name) is not null;
    }

    /// <summary>The file paths of the assemblies in the list named <paramref name="name"/>; none when there is no such list.</summary>
    public IReadOnlyList<AssemblyPath> GetPaths(string name)
    {
        var list = FindList(name);
        if (list is null)
        {
            return [];
        }

        return list.Paths;
    }

    /// <summary>Opens the list named <paramref name="name"/>. False when it is the open one already or there is none.</summary>
    public bool TryActivate(string name)
    {
        var list = FindList(name);
        if (list is null)
        {
            return false;
        }

        if (list == _active)
        {
            return false;
        }

        _active = list;
        NoteChange();
        return true;
    }

    /// <summary>Records <paramref name="paths"/> as the open list's assemblies.</summary>
    public void RememberActivePaths(IReadOnlyList<AssemblyPath> paths)
    {
        if (_active.Paths.SequenceEqual(paths))
        {
            return;
        }

        _active.Paths.Clear();
        _active.Paths.AddRange(paths);
        NoteChange();
    }

    /// <summary>Records <paramref name="analyzed"/> as the symbols analyzed in the open list.</summary>
    public void RememberActiveAnalyses(IReadOnlyList<CodeAnchor> analyzed)
    {
        if (_active.Analyzed.SequenceEqual(analyzed))
        {
            return;
        }

        _active.Analyzed.Clear();
        _active.Analyzed.AddRange(analyzed);
        NoteChange();
    }

    /// <summary>Keeps <paramref name="documents"/>, or none when null, as the documents the open list has open.</summary>
    public void RememberActiveDocuments(DocumentsRecord? documents)
    {
        _active.Documents = documents;
    }

    /// <summary>Records <paramref name="pdbs"/> as the PDBs picked for the open list's assemblies.</summary>
    public void RememberActivePdbs(IReadOnlyDictionary<AssemblyPath, string> pdbs)
    {
        RememberPicks(_active.Pdbs, pdbs);
    }

    /// <summary>Records <paramref name="frameworks"/> as the frameworks picked for the open list's assemblies.</summary>
    public void RememberActiveFrameworks(IReadOnlyDictionary<AssemblyPath, TargetFrameworkId> frameworks)
    {
        RememberPicks(_active.Frameworks, frameworks);
    }

    private void RememberPicks<T>(Dictionary<AssemblyPath, T> kept, IReadOnlyDictionary<AssemblyPath, T> picks) where T : class
    {
        if (HasPicks(kept, picks))
        {
            return;
        }

        kept.Clear();
        CopyPicks(picks, kept);
        NoteChange();
    }

    private static bool HasPicks<T>(Dictionary<AssemblyPath, T> kept, IReadOnlyDictionary<AssemblyPath, T> picks) where T : class
    {
        if (kept.Count != picks.Count)
        {
            return false;
        }

        return picks.All(p => Equals(kept.GetValueOrDefault(p.Key), p.Value));
    }

    /// <summary>Adds an empty list named <paramref name="name"/>, numbered when that name is taken,
    /// and returns the name it got.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public string Add(string name)
    {
        return Insert(new SavedList { Name = name, Paths = new List<AssemblyPath>(), Analyzed = new List<CodeAnchor>() });
    }

    /// <summary>Adds a list of the assemblies at <paramref name="paths"/>, named <paramref name="name"/>
    /// or numbered when that is taken, and returns the name it got.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public string Add(string name, IReadOnlyList<AssemblyPath> paths)
    {
        return Insert(new SavedList { Name = name, Paths = paths.ToList(), Analyzed = new List<CodeAnchor>() });
    }

    /// <summary>Adds a list with the assemblies, analyses and picks of the one named
    /// <paramref name="source"/>, named <paramref name="name"/> or numbered when that is taken, and
    /// returns the name it got.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public string Duplicate(string source, string name)
    {
        var copy = new SavedList { Name = name, Paths = GetPaths(source).ToList(), Analyzed = GetAnalyses(source).ToList() };
        var original = FindList(source);
        if (original is not null)
        {
            CopyPicksOf(original, copy);
        }

        return Insert(copy);
    }

    private static void CopyPicksOf(SavedList original, SavedList copy)
    {
        CopyPicks(original.Pdbs, copy.Pdbs);
        CopyPicks(original.Frameworks, copy.Frameworks);
    }

    private IReadOnlyList<CodeAnchor> GetAnalyses(string name)
    {
        var list = FindList(name);
        if (list is null)
        {
            return [];
        }

        return list.Analyzed;
    }

    private string Insert(SavedList list)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(list.Name);
        list.Name = MakeUniqueName(list.Name.Trim());
        _lists.Add(list);
        NoteChange();
        return list.Name;
    }

    private string MakeUniqueName(string name)
    {
        var existing = FindList(name);
        if (existing is null)
        {
            return name;
        }

        var number = 2;
        while (true)
        {
            var taken = FindList($"{name} {number}");
            if (taken is null)
            {
                break;
            }

            number++;
        }

        return $"{name} {number}";
    }

    /// <summary>Whether the list named <paramref name="name"/> may be renamed to <paramref name="newName"/>:
    /// it is not empty and no other list has it.</summary>
    public bool CanRename(string name, string newName)
    {
        var trimmed = newName.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var holder = FindList(trimmed);
        if (holder is null)
        {
            return true;
        }

        return holder == FindList(name);
    }

    /// <summary>Renames the list named <paramref name="name"/>, unless <see cref="CanRename"/> says no.</summary>
    public bool TryRename(string name, string newName)
    {
        if (!CanRename(name, newName))
        {
            return false;
        }

        var list = FindList(name);
        if (list is null)
        {
            return false;
        }

        list.Name = newName.Trim();
        NoteChange();
        return true;
    }

    /// <summary>Removes the list named <paramref name="name"/>. When it was the open one, the list
    /// that took its place opens, or the one before it when it was last.</summary>
    public void Remove(string name)
    {
        var list = FindList(name);
        if (list is null)
        {
            return;
        }

        var replacement = GetReplacement(list.Name);
        _lists.Remove(list);
        if (_lists.Count == 0)
        {
            _lists.Add(CreateEmpty(DefaultName));
        }

        if (list == _active)
        {
            _active = GetRemainingList(replacement);
        }

        NoteChange();
    }

    /// <summary>The name of the list that opens when the open list named <paramref name="name"/> is
    /// deleted: the one after it, else the one before, else a new empty <see cref="DefaultName"/>.</summary>
    public string GetReplacement(string name)
    {
        var list = FindList(name);
        if (list is null)
        {
            return Active;
        }

        var index = _lists.IndexOf(list);
        if (index + 1 < _lists.Count)
        {
            return _lists[index + 1].Name;
        }

        if (index > 0)
        {
            return _lists[index - 1].Name;
        }

        return DefaultName;
    }

    private SavedList GetRemainingList(string name)
    {
        var list = FindList(name);
        if (list is null)
        {
            throw new UnreachableException($"No list named {name} is left, though the list that takes a deleted one's place always is.");
        }

        return list;
    }

    private SavedList? FindList(string? name)
    {
        if (name is null)
        {
            return null;
        }

        return _lists.Find(l => string.Equals(l.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    // So that what was saved can tell it is out of date.
    private void NoteChange()
    {
        Revision++;
    }
}
