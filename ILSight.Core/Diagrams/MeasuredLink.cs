namespace ILSight.Core.Diagrams;

/// <summary>A link of a diagram with the size of its label, zero when it has none.</summary>
public readonly record struct MeasuredLink(TypeLink Link, float LabelWidth, float LabelHeight);
