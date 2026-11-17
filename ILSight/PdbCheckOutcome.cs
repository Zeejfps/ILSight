using ILSight.Core;

namespace ILSight;

/// <summary>What one check of a PDB picked for an assembly came to.</summary>
public union PdbCheckOutcome(PdbCheckOutcome.Checked, PdbCheckOutcome.Closed, PdbCheckOutcome.Failed)
{
    /// <summary>The PDB was read: whether it can be read for the assembly.</summary>
    public sealed record Checked
    {
        public required PdbCheck Check { get; init; }
    }

    /// <summary>The assembly, the file <see cref="Assembly"/>, was closed before it was read: nothing was checked.</summary>
    public sealed record Closed
    {
        public required string Assembly { get; init; }
    }

    public sealed record Failed
    {
        public required Exception Error { get; init; }
    }
}
