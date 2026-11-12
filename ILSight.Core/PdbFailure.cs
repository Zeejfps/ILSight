namespace ILSight.Core;

/// <summary>An assembly no PDB was written for, and why.</summary>
public abstract record PdbFailure
{
    private PdbFailure()
    {
    }

    public abstract LoadedModule Assembly { get; init; }

    /// <summary>Its debug directory names no PDB for one to match.</summary>
    public sealed record NoDebugEntry(LoadedModule Assembly) : PdbFailure;

    public sealed record NotWritten(LoadedModule Assembly, ExportError Error) : PdbFailure;
}
