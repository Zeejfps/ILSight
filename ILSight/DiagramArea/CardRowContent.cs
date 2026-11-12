using System.Diagnostics;
using ILSight.Core;

namespace ILSight.DiagramArea;

/// <summary>What a row of a card shows, with what only that kind of row has.</summary>
public abstract record CardRowContent
{
    private CardRowContent()
    {
    }

    /// <summary>The icon and name of the card's type, which a click opens.</summary>
    public sealed record Name(NodeIcon Icon, NodeAccessibility Accessibility, CodeReference.Entity Type) : CardRowContent;

    /// <summary>Whether the type is abstract, static or an interface.</summary>
    public sealed record Stereotype : CardRowContent;

    /// <summary>The title of <see cref="Part"/>, which folds, with how many members it holds.</summary>
    public sealed record Fold(CardFold Part, bool IsOpen) : CardRowContent;

    /// <summary>A member's icon and label, which a click opens.</summary>
    public sealed record Member(NodeIcon Icon, NodeAccessibility Accessibility, CodeReference.Entity Reference) : CardRowContent;

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Name, TResult> name, Func<Stereotype, TResult> stereotype,
        Func<Fold, TResult> fold, Func<Member, TResult> member)
    {
        return this switch
        {
            Name c => name(c),
            Stereotype c => stereotype(c),
            Fold c => fold(c),
            Member c => member(c),
            _ => throw new UnreachableException($"CardRowContent.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Name, TResult> name,
        Func<TState, Stereotype, TResult> stereotype, Func<TState, Fold, TResult> fold,
        Func<TState, Member, TResult> member)
    {
        return this switch
        {
            Name c => name(state, c),
            Stereotype c => stereotype(state, c),
            Fold c => fold(state, c),
            Member c => member(state, c),
            _ => throw new UnreachableException($"CardRowContent.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which it
    /// is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Name> name, Action<TState, Stereotype> stereotype,
        Action<TState, Fold> fold, Action<TState, Member> member)
    {
        switch (this)
        {
            case Name c:
                name(state, c);
                return;
            case Stereotype c:
                stereotype(state, c);
                return;
            case Fold c:
                fold(state, c);
                return;
            case Member c:
                member(state, c);
                return;
            default:
                throw new UnreachableException($"CardRowContent.Switch does not handle {GetType().Name}.");
        }
    }
}
