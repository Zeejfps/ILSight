using System.Diagnostics;
using ILSight.Core;

namespace ILSight;

/// <summary>What one check of a PDB picked for an assembly came to.</summary>
public abstract record PdbCheckOutcome
{
    private PdbCheckOutcome()
    {
    }

    /// <summary>The PDB was read: whether it can be read for the assembly.</summary>
    public sealed record Checked : PdbCheckOutcome
    {
        public required PdbCheck Check { get; init; }
    }

    /// <summary>The assembly, the file <see cref="Assembly"/>, was closed before it was read: nothing was checked.</summary>
    public sealed record Closed : PdbCheckOutcome
    {
        public required string Assembly { get; init; }
    }

    public sealed record Failed : PdbCheckOutcome
    {
        public required Exception Error { get; init; }
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Checked, TResult> isChecked, Func<Closed, TResult> closed, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Checked c => isChecked(c),
            Closed c => closed(c),
            Failed f => failed(f),
            _ => throw new UnreachableException($"PdbCheckOutcome.Match does not handle {GetType().Name}."),
        };
    }
}
