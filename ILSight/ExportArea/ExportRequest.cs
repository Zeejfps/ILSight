using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>An export of open assemblies as the export dialog asks for it: the folder it goes into,
/// the projects, what each is written with besides its code, and for several, the solution's file name.</summary>
public sealed record ExportRequest
{
    public required string Folder { get; init; }

    public required List<SolutionProject> Projects { get; init; }

    public required ProjectExtras Extras { get; init; }

    public required string SolutionFileName { get; init; }

    /// <summary>Whether the export writes a solution: there is more than one project.</summary>
    public bool IsSolution => Projects.Count > 1;
}
