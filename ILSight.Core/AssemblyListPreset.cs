namespace ILSight.Core;

/// <summary>A ready-made assembly list offered to add: the assemblies of a framework installed here.</summary>
public sealed record AssemblyListPreset(string Name, IReadOnlyList<AssemblyPath> Paths);
