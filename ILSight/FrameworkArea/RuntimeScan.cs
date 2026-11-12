using System.Diagnostics;
using ILSight.Core;

namespace ILSight.FrameworkArea;

/// <summary>How far scanning the runtime for its assemblies has got.</summary>
public abstract record RuntimeScan
{
    private RuntimeScan()
    {
    }

    public sealed record NotStarted : RuntimeScan;

    public sealed record Scanning : RuntimeScan;

    public sealed record Scanned(IReadOnlyList<RuntimeAssembly> Assemblies) : RuntimeScan;

    public sealed record Failed(string Error) : RuntimeScan;

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
            _ => throw new UnreachableException($"RuntimeScan.Match does not handle {GetType().Name}."),
        };
    }
}
