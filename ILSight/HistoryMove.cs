using ILSight.Core;
using ILSight.DiagramArea;

namespace ILSight;

/// <summary>What going to a history entry (<see cref="Navigation.GoToEntry"/>) left for the edge to start: the load of
/// the node the tab shows again, the lookup of the place the entry recorded, which the tab no longer shows, the read of
/// the cards of a diagram made again, or the run of a comparison made again. Its consumer takes each kind through a
/// handler of its own, switching over every case.</summary>
[MustUse]
public union HistoryMove(HistoryMove.Decompile, HistoryMove.LookUp, HistoryMove.ReadCards, HistoryMove.Compare)
{
    /// <summary>The tab shows the entry's node again and loads it.</summary>
    public sealed class Decompile
    {
        public required DecompileTicket Load { get; init; }
    }

    /// <summary>The entry's place is to be looked up for the request <see cref="Ticket"/> began, then shown at
    /// <see cref="Position"/> (<see cref="GoToHistoryEntryHandler"/>).</summary>
    public sealed class LookUp
    {
        public required NodePath Place { get; init; }

        public required ViewPosition? Position { get; init; }

        public required ResolveTicket Ticket { get; init; }
    }

    /// <summary>The entry's diagram, which no tab showed, is made again in a tab of its own, and the cards of its
    /// types are to be read (<see cref="ReopenDiagramHandler"/>).</summary>
    public sealed class ReadCards
    {
        public required DiagramPlaceRead Read { get; init; }
    }

    /// <summary>The entry's comparison, which no tab showed, is made again in a tab of its own, and is to be run
    /// (<see cref="AssemblyArea.CompareAssembliesHandler"/>).</summary>
    public sealed class Compare
    {
        public required ComparisonRun Run { get; init; }
    }
}
