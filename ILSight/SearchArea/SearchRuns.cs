namespace ILSight.SearchArea;

/// <summary>The runs of a search that may still deliver hits: asked for while the screen drew, until the edge starts
/// them (<see cref="RunSearchHandler"/>), then running until they end, or fail. A new query cancels them all, and only
/// a run among them is current, delivering hits and failing the search.</summary>
public sealed class SearchRuns
{
    private readonly List<SearchRun> _current = [];
    private readonly List<SearchRun> _asked = [];

    /// <summary>Whether a run may still deliver hits.</summary>
    public bool IsRunning => _current.Count > 0;

    /// <summary>Why a current run failed, its hits so far kept; null while none failed since the query was last
    /// replaced.</summary>
    public string? Failure { get; private set; }

    /// <summary>Whether runs were asked for that have not been started yet.</summary>
    public bool HasAsked => _asked.Count > 0;

    /// <summary>Asks for <paramref name="run"/> to be started; it is current from now on.</summary>
    public void Ask(SearchRun run)
    {
        _current.Add(run);
        _asked.Add(run);
    }

    /// <summary>The runs asked for since the last time, for the edge to start.</summary>
    public List<SearchRun> TakeAsked()
    {
        var asked = _asked.ToList();
        _asked.Clear();
        return asked;
    }

    /// <summary>Whether <paramref name="run"/> was not replaced by a new query.</summary>
    public bool IsCurrent(SearchRun run)
    {
        return _current.Contains(run);
    }

    /// <summary>Cancels every run, as a new query replaces them; those not started yet are never started.</summary>
    public void CancelAll()
    {
        foreach (var run in _current)
        {
            run.Work.Cancel();
        }

        _current.Clear();
        _asked.Clear();
        Failure = null;
    }

    /// <summary>Ends <paramref name="run"/>, which delivered all it found.</summary>
    public void End(SearchRun run)
    {
        _current.Remove(run);
    }

    /// <summary>Ends <paramref name="run"/>, which failed with <paramref name="why"/>, the search failed with it unless
    /// a new query replaced the run.</summary>
    public void Fail(SearchRun run, string why)
    {
        if (!_current.Remove(run))
        {
            return;
        }

        Failure = why;
    }
}
