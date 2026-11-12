namespace ILSight;

/// <summary>How a diagram was viewed, as the settings keep it: the point of it at the view's top-left and
/// the zoom, the parts of its cards folded or opened against how they start, and whether members hidden
/// from other assemblies showed.</summary>
public sealed record DiagramViewRecord(float X, float Y, float Zoom, List<CardFold>? Folds, bool ShowsHiddenMembers);
