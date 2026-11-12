namespace ILSight.Core.Diagrams;

/// <summary>A card of a diagram as wide and tall as it is drawn.</summary>
public readonly record struct MeasuredCard(CodeReference.Entity Type, float Width, float Height);
