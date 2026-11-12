using System.Diagnostics;

namespace ILSight.Core;

/// <summary>Where the children of a node stand: not asked for yet, being built, built, or failed to build.</summary>
public abstract record ChildrenState
{
    private ChildrenState()
    {
    }

    public sealed record Unloaded : ChildrenState;

    /// <summary>The one state of children not asked for yet.</summary>
    public static readonly Unloaded NotLoaded = new();

    /// <summary>Being built; <see cref="Load"/> ends <see cref="Ready"/> or <see cref="Failed"/>.</summary>
    public sealed record Running(Task<ChildrenState> Load) : ChildrenState;

    public sealed record Ready(IReadOnlyList<BrowseNode> Children) : ChildrenState;

    public sealed record Failed(Exception Error) : ChildrenState;

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Unloaded, TResult> unloaded,
        Func<TState, Running, TResult> running, Func<TState, Ready, TResult> ready,
        Func<TState, Failed, TResult> failed)
    {
        return this switch
        {
            Unloaded c => unloaded(state, c),
            Running c => running(state, c),
            Ready c => ready(state, c),
            Failed c => failed(state, c),
            _ => throw new UnreachableException($"ChildrenState.Match does not handle {GetType().Name}."),
        };
    }
}
