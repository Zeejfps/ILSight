using ILSight.Core;
using ILSight.Core.Diagrams;

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
}
