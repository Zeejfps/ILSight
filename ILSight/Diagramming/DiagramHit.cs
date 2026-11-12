namespace ILSight.Diagramming;

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
    public sealed record Row(CardFace Face, int Index) : DiagramHit
    {
        public CardRow Value => Face.Rows[Index];
    }
}
