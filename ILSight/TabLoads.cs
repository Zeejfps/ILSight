namespace ILSight;

/// <summary>The loads a workflow started in tabs, for the edge that started it to start, each under the work of what
/// it loads into (<see cref="Commands.StartLoads"/>): nodes to decompile, and the cards of the diagrams reopened from
/// the open list's saved documents to read.</summary>
[MustUse]
public sealed class TabLoads
{
    /// <summary>No load started.</summary>
    public static TabLoads None { get; } = new TabLoads { Decompiles = [], DiagramReads = [] };

    public required IReadOnlyList<DecompileTicket> Decompiles { get; init; }

    public required IReadOnlyList<SavedDiagramRead> DiagramReads { get; init; }

    /// <summary>These loads, then those of <paramref name="other"/>.</summary>
    public TabLoads With(TabLoads other)
    {
        return new TabLoads { Decompiles = [.. Decompiles, .. other.Decompiles], DiagramReads = [.. DiagramReads, .. other.DiagramReads] };
    }

    /// <summary>These loads, then <paramref name="decompiles"/>.</summary>
    public TabLoads With(IEnumerable<DecompileTicket> decompiles)
    {
        return new TabLoads { Decompiles = [.. Decompiles, .. decompiles], DiagramReads = DiagramReads };
    }

    /// <summary>These loads, then <paramref name="decompile"/> when one started.</summary>
    public TabLoads With(DecompileTicket? decompile)
    {
        if (decompile is null)
        {
            return this;
        }

        return With([decompile]);
    }
}
