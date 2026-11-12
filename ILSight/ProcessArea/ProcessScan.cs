using System.Diagnostics;
using ILSight.Core.Processes;

namespace ILSight.ProcessArea;

/// <summary>How far looking for the running processes has got.</summary>
public abstract record ProcessScan
{
    private ProcessScan()
    {
    }

    public sealed record NotStarted : ProcessScan;

    public sealed record Scanning : ProcessScan;

    public sealed record Scanned(IReadOnlyList<RunningProcess> Processes) : ProcessScan;

    public sealed record Failed(string Error) : ProcessScan;

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, NotStarted, TResult> notStarted,
        Func<TState, Scanning, TResult> scanning, Func<TState, Scanned, TResult> scanned,
        Func<TState, Failed, TResult> failed)
    {
        return this switch
        {
            NotStarted c => notStarted(state, c),
            Scanning c => scanning(state, c),
            Scanned c => scanned(state, c),
            Failed c => failed(state, c),
            _ => throw new UnreachableException($"ProcessScan.Match does not handle {GetType().Name}."),
        };
    }
}
