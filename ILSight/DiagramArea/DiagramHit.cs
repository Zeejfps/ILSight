namespace ILSight.DiagramArea;

/// <summary>What a press on a diagram landed on: nothing, in the space between the cards; a card, off its
/// rows; or a row of a card.</summary>
public union DiagramHit(DiagramHit.Nothing, DiagramHit.Card, DiagramHit.Row)
{
    public sealed record Nothing;

    public sealed record Card(CardFace Face);

    /// <summary>The row <see cref="Index"/> of <see cref="Face"/>.</summary>
    public sealed record Row
    {
        public required CardFace Face { get; init; }

        public required int Index { get; init; }

        public CardRow Value => Face.Rows[Index];
    }
}
