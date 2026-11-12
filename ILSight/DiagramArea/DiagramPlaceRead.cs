using ILSight.Core;

namespace ILSight.DiagramArea;

/// <summary>A read going back to a diagram no tab showed any more started (<see cref="Navigation.GoToEntry"/>), for
/// <see cref="ReopenDiagramHandler"/> to bring in: the cards of <see cref="Types"/>, the types the diagram had as it was
/// left, for <see cref="Diagram"/>, made again in a tab of its own, whose closing cancels the read.</summary>
[MustUse]
public sealed class DiagramPlaceRead
{
    public required TypeDiagram Diagram { get; init; }

    public required IReadOnlyList<TypeAnchor> Types { get; init; }

    public required string Title { get; init; }
}
