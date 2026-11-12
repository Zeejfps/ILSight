using ILSight.BookmarkArea;

namespace ILSight;

/// <summary>Where a tab's view stands once what it shows arrives: back at <see cref="Position"/>, else on
/// the line <see cref="Target"/> marks or the grid's <see cref="Row"/>, else on the node's definition.</summary>
public readonly record struct Arrival
{
    public ViewPosition? Position { get; init; }

    public Bookmark? Target { get; init; }

    public int? Row { get; init; }
}
