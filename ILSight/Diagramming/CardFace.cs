using ILSight.Core.Diagrams;

namespace ILSight.Diagramming;

/// <summary>How a card is drawn at the diagram's own size: its rows from the top, how wide and tall it is,
/// and how far down its header reaches.</summary>
public sealed record CardFace(TypeCard Card, IReadOnlyList<CardRow> Rows, float Width, float Height, float HeaderHeight);
