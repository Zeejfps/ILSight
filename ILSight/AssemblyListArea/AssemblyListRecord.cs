using ILSight.Core;

namespace ILSight.AssemblyListArea;

/// <summary>A named assembly list as the settings keep it: its name, its assemblies' file paths, in
/// order, the symbols analyzed in it, by assembly path the PDBs picked for its assemblies and the
/// frameworks picked to resolve their references against, and the documents it had open.</summary>
public sealed record AssemblyListRecord
{
    public required string Name { get; init; }

    public required List<AssemblyPath> Assemblies { get; init; }

    public List<CodeAnchor>? Analyzed { get; init; }

    public Dictionary<AssemblyPath, string>? Pdbs { get; init; }

    public Dictionary<AssemblyPath, string>? Frameworks { get; init; }

    public DocumentsRecord? Documents { get; init; }
}
