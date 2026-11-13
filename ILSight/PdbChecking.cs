using System.Diagnostics;
using ILSight.Core;

namespace ILSight;

/// <summary>What checking a PDB picked for an assembly came to.</summary>
public abstract record PdbChecking
{
    private PdbChecking()
    {
    }

    /// <summary>The PDB was read: whether it can be read for the assembly.</summary>
    public sealed record Checked : PdbChecking
    {
        public required PdbCheck Check { get; init; }
    }

    /// <summary>The assembly, the file <see cref="Assembly"/>, was closed before it was read: nothing was checked.</summary>
    public sealed record Closed : PdbChecking
    {
        public required string Assembly { get; init; }
    }

    /// <summary>The check failed.</summary>
    public sealed record Failed : PdbChecking
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
            _ => throw new UnreachableException($"PdbChecking.Match does not handle {GetType().Name}."),
        };
    }
}
