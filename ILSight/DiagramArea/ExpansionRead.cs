using ILSight.Core;
using ILSight.Core.DiagramArea;

namespace ILSight.DiagramArea;

/// <summary>A read of the cards a card's links lead to, started from its menu, for <see cref="DiagramGrower"/> to
/// bring in: on the diagram, which counts the read as under way until it ends or fails, and whose closing cancels
/// it.</summary>
[MustUse]
public sealed class ExpansionRead
{
    public required TypeDiagram Diagram { get; init; }

    /// <summary>The read <see cref="Diagram"/> counts as under way until it ends or fails.</summary>
    public required CardRead Read { get; init; }

    public required TypeCard Card { get; init; }

    /// <summary>Starts a read of <paramref name="card"/>'s cards on <paramref name="diagram"/>.</summary>
    public static ExpansionRead Start(TypeDiagram diagram, TypeCard card)
    {
        return new ExpansionRead { Diagram = diagram, Read = diagram.StartReading(), Card = card };
    }
}
