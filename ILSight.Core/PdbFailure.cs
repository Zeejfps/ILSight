namespace ILSight.Core;

/// <summary>An assembly no PDB was written for, and why.</summary>
public union PdbFailure(PdbFailure.NoDebugEntry, PdbFailure.NotWritten)
{
    /// <summary>Its debug directory names no PDB for one to match.</summary>
    public sealed record NoDebugEntry
    {
        public required LoadedModule Assembly { get; init; }
    }

    public sealed record NotWritten
    {
        public required LoadedModule Assembly { get; init; }

        public required ExportError Error { get; init; }
    }

    /// <summary>The assembly no PDB was written for.</summary>
    public LoadedModule Assembly => this switch
    {
        NoDebugEntry noDebugEntry => noDebugEntry.Assembly,
        NotWritten notWritten => notWritten.Assembly,
    };
}
