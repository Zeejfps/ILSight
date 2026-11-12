using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Processes;

namespace ILSight.ProcessArea;

/// <summary>How far listing the selected process's modules has got. Each case past <see cref="None"/> names
/// its process, and those that ended name its entry assembly, which a failed listing can still find from the
/// command line.</summary>
public abstract record ModuleScan
{
    private ModuleScan()
    {
    }

    /// <summary>No process is selected.</summary>
    public sealed record None : ModuleScan;

    public sealed record Loading(RunningProcess Process) : ModuleScan;

    public sealed record Loaded(RunningProcess Process, IReadOnlyList<RunningModule> Modules, AssemblyPath? EntryAssembly) : ModuleScan;

    /// <summary>The process could not be asked: it is gone, refused, or didn't answer in time.</summary>
    public sealed record Unreachable(RunningProcess Process, string Detail, AssemblyPath? EntryAssembly) : ModuleScan;

    /// <summary>The process answered with what could not be read.</summary>
    public sealed record Unreadable(RunningProcess Process, string Detail, AssemblyPath? EntryAssembly) : ModuleScan;

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<None, TResult> none, Func<Loading, TResult> loading,
        Func<Loaded, TResult> loaded, Func<Unreachable, TResult> unreachable, Func<Unreadable, TResult> unreadable)
    {
        return this switch
        {
            None c => none(c),
            Loading c => loading(c),
            Loaded c => loaded(c),
            Unreachable c => unreachable(c),
            Unreadable c => unreadable(c),
            _ => throw new UnreachableException($"ModuleScan.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, None, TResult> none,
        Func<TState, Loading, TResult> loading, Func<TState, Loaded, TResult> loaded,
        Func<TState, Unreachable, TResult> unreachable, Func<TState, Unreadable, TResult> unreadable)
    {
        return this switch
        {
            None c => none(state, c),
            Loading c => loading(state, c),
            Loaded c => loaded(state, c),
            Unreachable c => unreachable(state, c),
            Unreadable c => unreadable(state, c),
            _ => throw new UnreachableException($"ModuleScan.Match does not handle {GetType().Name}."),
        };
    }
}
