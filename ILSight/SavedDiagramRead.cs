using ILSight.DiagramArea;

namespace ILSight;

/// <summary>A read <see cref="Diagrams.ReopenDiagram"/> started, for <see cref="ReopenDiagramHandler"/> to bring in:
/// the cards of the types <see cref="Saved"/> kept, for the diagram shown in the tab <see cref="Reopened"/> waits in,
/// which counts the read as under way until it ends, and whose closing cancels it.</summary>
[MustUse]
public sealed class SavedDiagramRead
{
    public required ReopenedTab Reopened { get; init; }

    public required TypeDiagram Diagram { get; init; }

    public required DiagramDocumentRecord Saved { get; init; }
}
