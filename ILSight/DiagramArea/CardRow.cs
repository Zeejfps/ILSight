using ILSight.Core;

namespace ILSight.DiagramArea;

/// <summary>A row of a card at the diagram's own size: what it shows, how tall it is and how far in its
/// content starts, its text, where it starts below the card's top, and how wide its text is.</summary>
public readonly record struct CardRow
{
    public required CardRowContent Content { get; init; }

    public required float Height { get; init; }

    public required float Indent { get; init; }

    public required string Text { get; init; }

    public float Top { get; init; } = 0f;

    public float TextWidth { get; init; } = 0f;

    public CardRow()
    {
    }

    public float Bottom => Top + Height;

    /// <summary>The type or member whose code a click on the row opens, or null for a row that opens none.</summary>
    public CodeReference.Entity? Target => Content.Match<CodeReference.Entity?>(
        name: static name => name.Type,
        stereotype: static _ => null,
        fold: static _ => null,
        member: static member => member.Reference);

    /// <summary>Whether a click on the row opens the code of what it shows.</summary>
    public bool IsLink => Target != null;
}
