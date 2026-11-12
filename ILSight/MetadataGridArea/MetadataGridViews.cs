namespace ILSight.MetadataGridArea;

/// <summary>The sorts and filters the grids asked for while the screen drew, which the frame edge starts off the
/// screen's thread (<see cref="ArrangeMetadataGridHandler"/>): one per grid, its newest.</summary>
public sealed class MetadataGridViews
{
    private readonly List<MetadataGridViewAsk> _asked = [];

    public bool HasAsked => _asked.Count > 0;

    /// <summary>Asks for <paramref name="ask"/>, in place of what its grid asked for before in this frame.</summary>
    public void Ask(MetadataGridViewAsk ask)
    {
        Forget(ask.State);
        _asked.Add(ask);
    }

    /// <summary>Drops what <paramref name="state"/> asked for and nobody started yet.</summary>
    public void Forget(MetadataGridState state)
    {
        _asked.RemoveAll(a => a.State == state);
    }

    public List<MetadataGridViewAsk> TakeAsked()
    {
        var asked = _asked.ToList();
        _asked.Clear();
        return asked;
    }
}
