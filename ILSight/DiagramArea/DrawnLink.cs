using ILSight.Core.Diagrams;

namespace ILSight.DiagramArea;

/// <summary>A link as one frame draws it: how it relates its types, its route on the diagram, and its label
/// with the label's size at the diagram's own size, when it has one.</summary>
internal readonly record struct DrawnLink(TypeLinkKind Kind, LinkRoute Route, string? Label, float LabelWidth, float LabelHeight);
