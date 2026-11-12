
namespace ILSight.DiagramArea;

/// <summary>How a diagram was viewed, as a settings file can hold it.</summary>
internal sealed record DiagramViewJson
{
    public float X { get; init; }

    public float Y { get; init; }

    public float Zoom { get; init; }

    public List<CardFoldJson?>? Folds { get; init; }

    public bool ShowsHiddenMembers { get; init; }
}
