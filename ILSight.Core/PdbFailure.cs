using System.Diagnostics;

namespace ILSight.Core;

/// <summary>An assembly no PDB was written for, and why.</summary>
public abstract record PdbFailure
{
    private PdbFailure()
    {
    }

    public required LoadedModule Assembly { get; init; }

    /// <summary>Its debug directory names no PDB for one to match.</summary>
    public sealed record NoDebugEntry : PdbFailure;

    public sealed record NotWritten : PdbFailure
    {
        public required ExportError Error { get; init; }
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<NoDebugEntry, TResult> noDebugEntry, Func<NotWritten, TResult> notWritten)
    {
        return this switch
        {
            NoDebugEntry c => noDebugEntry(c),
            NotWritten c => notWritten(c),
            _ => throw new UnreachableException($"PdbFailure.Match does not handle {GetType().Name}."),
        };
    }
}
