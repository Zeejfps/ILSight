using ILSight.Core;
using ILSight.Core.DiagramArea;
using ILSight.DiagramArea;

namespace ILSight;

/// <summary>A part of a card that folds: its own members of one kind, or what it inherits from one base
/// type. A key of <see cref="DiagramViewState.FlippedFolds"/>, so compared by value.</summary>
public union CardFold(CardFold.Own, CardFold.Inherited)
{
    /// <summary>The members of <see cref="Card"/> of the kind <see cref="Section"/>.</summary>
    public sealed record Own
    {
        public required TypeAnchor Card { get; init; }

        public required MemberSection Section { get; init; }
    }

    /// <summary>What <see cref="Card"/> inherits from the base type named <see cref="From"/>.</summary>
    public sealed record Inherited
    {
        public required TypeAnchor Card { get; init; }

        public required string From { get; init; }
    }
}
