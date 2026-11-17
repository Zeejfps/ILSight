namespace ILSight.Core;

/// <summary>What came of the PDB of an exported project.</summary>
public union ProjectPdb(ProjectPdb.NotAsked, ProjectPdb.Written, ProjectPdb.Failed)
{
    public sealed record NotAsked;

    public sealed record Written(string Path);

    public sealed record Failed(PdbFailure Failure);
}
