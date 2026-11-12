using System.Diagnostics;
using ILSight.Core;
using ILSight.DiagramArea;

namespace ILSight;

/// <summary>What going to a history entry (<see cref="Navigation.GoToEntry"/>) left for the edge to start: the load of
/// the node the tab shows again, the lookup of the place the entry recorded, which the tab no longer shows, the read of
/// the cards of a diagram made again, or the run of a comparison made again. Its consumer takes each kind through a
/// handler of its own (<see cref="Switch"/>).</summary>
[MustUse]
public abstract class HistoryMove
{
    private HistoryMove()
    {
    }

    /// <summary>The tab shows the entry's node again and loads it.</summary>
    public sealed class Decompile : HistoryMove
    {
        public required DecompileTicket Load { get; init; }
    }

    /// <summary>The entry's place is to be looked up for the request <see cref="Ticket"/> began, then shown at
    /// <see cref="Position"/> (<see cref="GoToHistoryEntryHandler"/>).</summary>
    public sealed class LookUp : HistoryMove
    {
        public required NodePath Place { get; init; }

        public required ViewPosition? Position { get; init; }

        public required ResolveTicket Ticket { get; init; }
    }

    /// <summary>The entry's diagram, which no tab showed, is made again in a tab of its own, and the cards of its
    /// types are to be read (<see cref="ReopenDiagramHandler"/>).</summary>
    public sealed class ReadCards : HistoryMove
    {
        public required DiagramPlaceRead Read { get; init; }
    }

    /// <summary>The entry's comparison, which no tab showed, is made again in a tab of its own, and is to be run
    /// (<see cref="AssemblyArea.CompareAssembliesHandler"/>).</summary>
    public sealed class Compare : HistoryMove
    {
        public required ComparisonRun Run { get; init; }
    }

    /// <summary>Hands this to the handler for its kind: <paramref name="decompile"/> the load of the node shown
    /// again, <paramref name="lookUp"/> the place to look up, <paramref name="readCards"/> the cards of a diagram made
    /// again, <paramref name="compare"/> the run of a comparison made again.</summary>
    public void Switch(Action<DecompileTicket> decompile, Action<LookUp> lookUp, Action<DiagramPlaceRead> readCards, Action<ComparisonRun> compare)
    {
        switch (this)
        {
            case Decompile d:
                decompile(d.Load);
                break;
            case LookUp l:
                lookUp(l);
                break;
            case ReadCards r:
                readCards(r.Read);
                break;
            case Compare c:
                compare(c.Run);
                break;
            default:
                throw new UnreachableException($"HistoryMove.Switch does not handle {GetType().Name}.");
        }
    }
}
