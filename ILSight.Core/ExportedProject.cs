using ICSharpCode.Decompiler.Solution;

namespace ILSight.Core;

/// <summary>An assembly decompiled into a C# project: the project file, what could not be
/// decompiled, which the project holds as error comments instead, and what came of its PDB.</summary>
public sealed record ExportedProject(LoadedModule Assembly, string ProjectPath, IReadOnlyList<ExportError> Errors, ProjectId Id)
{
    public ProjectPdb Pdb { get; internal init; } = new ProjectPdb.NotAsked();
}
