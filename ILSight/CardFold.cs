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

    /// <summary>The members of <paramref name="Card"/> of the kind <paramref name="Section"/>.</summary>
    public sealed record Own(TypeAnchor Card, MemberSection Section) : CardFold;

    /// <summary>What <paramref name="Card"/> inherits from the base type named <paramref name="From"/>.</summary>
    public sealed record Inherited(TypeAnchor Card, string From) : CardFold;

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
