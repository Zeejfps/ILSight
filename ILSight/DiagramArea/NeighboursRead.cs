using ILSight.Core;

namespace ILSight.DiagramArea;

/// <summary>A read <see cref="Diagrams.ShowInDiagram"/> started, for <see cref="DiagramGrowing.AddWithNeighboursAsync"/>
/// to bring in: the type to show and the types of its assembly it is directly related to, on the diagram the tab
/// shows, which counts the read as under way until it ends, and whose closing cancels it.</summary>
[MustUse]
public sealed class NeighboursRead
{
    public required DocumentTab Tab { get; init; }

    public required TypeDiagram Diagram { get; init; }

    /// <summary>The read <see cref="Diagram"/> counts as under way until it ends.</summary>
    public required CardRead Read { get; init; }

    public required CodeReference.Entity Type { get; init; }
}
