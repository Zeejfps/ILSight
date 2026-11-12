using ICSharpCode.Decompiler.Solution;

namespace ILSight.Core;

/// <summary>An assembly decompiled into a C# project: the project file, what could not be
/// decompiled, which the project holds as error comments instead, and what came of its PDB.</summary>
public sealed record ExportedProject
{
    public required LoadedModule Assembly { get; init; }

    public required string ProjectPath { get; init; }

    public required IReadOnlyList<ExportError> Errors { get; init; }

    public required ProjectId Id { get; init; }

    public ProjectPdb Pdb { get; internal init; } = new ProjectPdb.NotAsked();
}
