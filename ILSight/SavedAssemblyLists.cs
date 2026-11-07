using ILSight.Core;

namespace ILSight;

/// <summary>A named assembly list as the settings keep it: its name, its assemblies' file paths, in
/// order, the symbols analyzed in it, by assembly path the PDBs picked for its assemblies and the
/// frameworks picked to resolve their references against, and the documents it had open.</summary>
public sealed record AssemblyListRecord(string Name, List<AssemblyPath> Assemblies, List<CodeAnchor>? Analyzed = null,
    Dictionary<AssemblyPath, string>? Pdbs = null, Dictionary<AssemblyPath, string>? Frameworks = null, DocumentsRecord? Documents = null);

/// <summary>
/// The user's named assembly lists and which one is open. Names are never empty and never the same
/// twice, ignoring case, and there is always a list open: deleting the last one leaves an empty one
/// named <see cref="DefaultName"/>.
/// </summary>
public sealed class SavedAssemblyLists
{
    public const string DefaultName = "Default";

    public required List<SavedList> Lists { get; init; }
    public required SavedList ActiveList { get; set; }

    /// <summary>Changes whenever a list is added, renamed, removed or opened, or its assemblies or analyses
    /// change; not when its documents do, which the open tabs count.</summary>
    public int Revision { get; set; }

    /// <summary>The name of the list that is open.</summary>
    public string Active => ActiveList.Name;

    /// <summary>The file paths of the open list's assemblies.</summary>
    public IReadOnlyList<AssemblyPath> ActivePaths => ActiveList.Paths;

    /// <summary>The symbols analyzed in the open list.</summary>
    public IReadOnlyList<CodeAnchor> ActiveAnalyses => ActiveList.Analyzed;

    /// <summary>The PDBs picked for the open list's assemblies, by assembly path.</summary>
    public IReadOnlyDictionary<AssemblyPath, string> ActivePdbs => ActiveList.Pdbs;

    /// <summary>The frameworks picked for the open list's assemblies, by assembly path.</summary>
    public IReadOnlyDictionary<AssemblyPath, TargetFrameworkId> ActiveFrameworks => ActiveList.Frameworks;

    /// <summary>The documents the open list had open when they were last kept with it, or null for none.</summary>
    public DocumentsRecord? ActiveDocuments => ActiveList.Documents;

    public IReadOnlyList<string> Names => Lists.ConvertAll(static l => l.Name);

    public int Count => Lists.Count;

    /// <summary>A list: its name, its assemblies' paths, the symbols analyzed in it, what was picked for
    /// its assemblies, by assembly path, and the documents it had open.</summary>
    public sealed class SavedList
    {
        public required string Name;
        public DocumentsRecord? Documents;
        public required List<AssemblyPath> Paths { get; init; }
        public required List<CodeAnchor> Analyzed { get; init; }
        public Dictionary<AssemblyPath, string> Pdbs { get; } = new();
        public Dictionary<AssemblyPath, TargetFrameworkId> Frameworks { get; } = new();
    }
}
