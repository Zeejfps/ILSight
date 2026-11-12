using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.DiagramArea;

namespace ILSight;

/// <summary>A part of a card that folds: its own members of one kind, or what it inherits from one base
/// type. A key of <see cref="DiagramViewState.FlippedFolds"/>, so compared by value.</summary>
public abstract record CardFold
{
    private CardFold()
    {
    }

    /// <summary>The members of <see cref="Card"/> of the kind <see cref="Section"/>.</summary>
    public sealed record Own : CardFold
    {
        public required TypeAnchor Card { get; init; }

        public required MemberSection Section { get; init; }
    }

    /// <summary>What <see cref="Card"/> inherits from the base type named <see cref="From"/>.</summary>
    public sealed record Inherited : CardFold
    {
        public required TypeAnchor Card { get; init; }

        public required string From { get; init; }
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Own, TResult> own, Func<Inherited, TResult> inherited)
    {
        return this switch
        {
            Own c => own(c),
            Inherited c => inherited(c),
            _ => throw new UnreachableException($"CardFold.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Own, TResult> own,
        Func<TState, Inherited, TResult> inherited)
    {
        return this switch
        {
            Own c => own(state, c),
            Inherited c => inherited(state, c),
            _ => throw new UnreachableException($"CardFold.Match does not handle {GetType().Name}."),
        };
    }
}
