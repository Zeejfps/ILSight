using ILSight.Core;

namespace ILSight.AssemblyListKeeping;

/// <summary>A named assembly list as the settings keep it: its name, its assemblies' file paths, in
/// order, the symbols analyzed in it, by assembly path the PDBs picked for its assemblies and the
/// frameworks picked to resolve their references against, and the documents it had open.</summary>
public sealed record AssemblyListRecord(string Name, List<AssemblyPath> Assemblies, List<CodeAnchor>? Analyzed = null,
    Dictionary<AssemblyPath, string>? Pdbs = null, Dictionary<AssemblyPath, string>? Frameworks = null, DocumentsRecord? Documents = null);
