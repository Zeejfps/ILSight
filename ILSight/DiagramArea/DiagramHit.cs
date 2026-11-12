using System.Diagnostics;

namespace ILSight.DiagramArea;

/// <summary>What a press on a diagram landed on: nothing, in the space between the cards; a card, off its
/// rows; or a row of a card.</summary>
public abstract record DiagramHit
{
    private DiagramHit()
    {
    }

    public sealed record Nothing : DiagramHit;

    public sealed record Card(CardFace Face) : DiagramHit;

    /// <summary>The row <see cref="Index"/> of <see cref="Face"/>.</summary>
    public sealed record Row : DiagramHit
    {
        public required CardFace Face { get; init; }

        public required int Index { get; init; }

        public CardRow Value => Face.Rows[Index];
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Nothing, TResult> nothing, Func<Card, TResult> card, Func<Row, TResult> row)
    {
        return this switch
        {
            Nothing c => nothing(c),
            Card c => card(c),
            Row c => row(c),
            _ => throw new UnreachableException($"DiagramHit.Match does not handle {GetType().Name}."),
        };
    }
}
