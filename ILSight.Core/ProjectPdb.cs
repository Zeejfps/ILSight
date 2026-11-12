namespace ILSight.Core;

/// <summary>What came of the PDB of an exported project.</summary>
public abstract record ProjectPdb
{
    private ProjectPdb()
    {
    }

    public sealed record NotAsked : ProjectPdb;

    public sealed record Written(string Path) : ProjectPdb;

    public sealed record Failed(PdbFailure Failure) : ProjectPdb;
}
