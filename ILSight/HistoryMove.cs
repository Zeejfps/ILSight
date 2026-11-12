using System.Diagnostics;
using ILSight.Core;

namespace ILSight;

/// <summary>What going to a history entry (<see cref="Navigation.GoToEntry"/>) left for the edge to start: the load of
/// the node the tab shows again, or the lookup of the place the entry recorded, which the tab no longer shows. Its
/// consumer takes each kind through a handler of its own (<see cref="Switch"/>).</summary>
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

    /// <summary>Hands this to the handler for its kind: <paramref name="decompile"/> the load of the node shown
    /// again, <paramref name="lookUp"/> the place to look up.</summary>
    public void Switch(Action<DecompileTicket> decompile, Action<LookUp> lookUp)
    {
        switch (this)
        {
            case Decompile d:
                decompile(d.Load);
                break;
            case LookUp l:
                lookUp(l);
                break;
            default:
                throw new UnreachableException($"HistoryMove.Switch does not handle {GetType().Name}.");
        }
    }
}
