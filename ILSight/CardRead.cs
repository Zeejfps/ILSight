namespace ILSight;

/// <summary>One read of cards a <see cref="TypeDiagram"/> counts as under way, from
/// <see cref="TypeDiagram.StartReading"/> until <see cref="TypeDiagram.FinishReading"/> is handed it; finishing it again
/// does nothing, so that whatever ends the read, its workflow or the edge settling it, ends only that one.</summary>
public sealed class CardRead
{
}
