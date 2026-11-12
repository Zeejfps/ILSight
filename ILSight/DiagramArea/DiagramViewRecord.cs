namespace ILSight.DiagramArea;

/// <summary>How a diagram was viewed, as the settings keep it: the point of it at the view's top-left and
/// the zoom, the parts of its cards folded or opened against how they start, and whether members hidden
/// from other assemblies showed.</summary>
public sealed record DiagramViewRecord
{
    public required float X { get; init; }

    public required float Y { get; init; }

    public required float Zoom { get; init; }

    public required List<CardFold>? Folds { get; init; }

    public required bool ShowsHiddenMembers { get; init; }
}
