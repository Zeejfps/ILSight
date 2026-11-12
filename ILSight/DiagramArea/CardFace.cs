using ILSight.Core.Diagrams;

namespace ILSight.DiagramArea;

/// <summary>How a card is drawn at the diagram's own size: its rows from the top, how wide and tall it is,
/// and how far down its header reaches.</summary>
public sealed record CardFace
{
    public required TypeCard Card { get; init; }

    public required IReadOnlyList<CardRow> Rows { get; init; }

    public required float Width { get; init; }

    public required float Height { get; init; }

    public required float HeaderHeight { get; init; }
}
