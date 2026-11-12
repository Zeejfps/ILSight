using ILSight.Commanding;

namespace ILSight;

/// <summary>The loads a workflow started in tabs, for the edge that started it to start, each under the work of what
/// it loads into (<see cref="Commands.StartLoads"/>): nodes to decompile, the cards of the diagrams reopened from
/// the open list's saved documents to read, the nodes the tabs reopened from them wait for to find, the comparisons
/// reopened from them to run, and the analyses reopened with the list to find. Its consumer takes each kind through a handler of its own (<see cref="Switch"/>), so a
/// kind added later does not compile at the edge until the edge starts it.</summary>
[MustUse]
public sealed class TabLoads
{
    private readonly List<DecompileTicket> _decompiles = [];
    private readonly List<SavedDiagramRead> _diagramReads = [];
    private readonly List<ReopenedNodesLookup> _nodeLookups = [];
    private readonly List<ComparisonRun> _comparisonRuns = [];
    private readonly List<AnalysesRestore> _analysesRestores = [];

    private TabLoads()
    {
    }

    /// <summary>No load started.</summary>
    public static TabLoads None { get; } = new TabLoads();

    /// <summary>The loads <paramref name="decompiles"/>, <paramref name="diagramReads"/>, <paramref name="nodeLookups"/>
    /// and <paramref name="comparisonRuns"/>.</summary>
    public static TabLoads From(IEnumerable<DecompileTicket> decompiles, IEnumerable<SavedDiagramRead> diagramReads,
        IEnumerable<ReopenedNodesLookup> nodeLookups, IEnumerable<ComparisonRun> comparisonRuns)
    {
        var loads = new TabLoads();
        loads._decompiles.AddRange(decompiles);
        loads._diagramReads.AddRange(diagramReads);
        loads._nodeLookups.AddRange(nodeLookups);
        loads._comparisonRuns.AddRange(comparisonRuns);
        return loads;
    }

    /// <summary>These loads, then those of <paramref name="other"/>.</summary>
    public TabLoads With(TabLoads other)
    {
        var loads = From([.. _decompiles, .. other._decompiles], [.. _diagramReads, .. other._diagramReads], [.. _nodeLookups, .. other._nodeLookups],
            [.. _comparisonRuns, .. other._comparisonRuns]);
        loads._analysesRestores.AddRange(_analysesRestores);
        loads._analysesRestores.AddRange(other._analysesRestores);
        return loads;
    }

    /// <summary>These loads, then <paramref name="decompiles"/>.</summary>
    public TabLoads With(IEnumerable<DecompileTicket> decompiles)
    {
        var loads = From([.. _decompiles, .. decompiles], _diagramReads, _nodeLookups, _comparisonRuns);
        loads._analysesRestores.AddRange(_analysesRestores);
        return loads;
    }

    /// <summary>These loads, then <paramref name="restore"/> when analyses wait to be found.</summary>
    public TabLoads With(AnalysesRestore? restore)
    {
        var loads = From(_decompiles, _diagramReads, _nodeLookups, _comparisonRuns);
        loads._analysesRestores.AddRange(_analysesRestores);
        if (restore is not null)
        {
            loads._analysesRestores.Add(restore);
        }

        return loads;
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

    /// <summary>Hands each load to the handler for its kind: <paramref name="decompile"/> each node to decompile,
    /// <paramref name="readCards"/> each reopened diagram whose cards are to be read, <paramref name="findNodes"/> each
    /// lookup of the nodes reopened tabs wait for, <paramref name="compare"/> each reopened comparison to run,
    /// <paramref name="restoreAnalyses"/> each set of reopened analyses to find.</summary>
    public void Switch(Action<DecompileTicket> decompile, Action<SavedDiagramRead> readCards, Action<ReopenedNodesLookup> findNodes,
        Action<ComparisonRun> compare, Action<AnalysesRestore> restoreAnalyses)
    {
        foreach (var ticket in _decompiles)
        {
            decompile(ticket);
        }

        foreach (var read in _diagramReads)
        {
            readCards(read);
        }

        foreach (var lookup in _nodeLookups)
        {
            findNodes(lookup);
        }

        foreach (var run in _comparisonRuns)
        {
            compare(run);
        }

        foreach (var restore in _analysesRestores)
        {
            restoreAnalyses(restore);
        }
    }
}
