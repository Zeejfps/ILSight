namespace ILSight.Core;

/// <summary>One step of a <see cref="NodePath"/>: what the node is, and the label it showed when
/// the path was taken, which describes the step but does not name it.</summary>
public readonly record struct NodePathStep(NodePathSegment Segment, string Label);
