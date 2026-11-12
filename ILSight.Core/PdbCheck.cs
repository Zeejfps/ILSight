namespace ILSight.Core;

/// <summary>Whether a PDB file picked for an assembly can be read for it.</summary>
public enum PdbCheck
{
    /// <summary>It is a portable PDB of this build of the assembly, or one the assembly names no build for.</summary>
    Matches,

    /// <summary>It is a portable PDB of another build of the assembly.</summary>
    OtherBuild,

    /// <summary>It is a Windows PDB, which cannot be read.</summary>
    WindowsPdb,

    /// <summary>It is no PDB, or it cannot be read.</summary>
    Unreadable,
}
