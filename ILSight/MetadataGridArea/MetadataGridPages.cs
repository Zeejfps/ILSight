using ILSight.Core.Metadata;

namespace ILSight.MetadataGridArea;

/// <summary>The pages of paged grids that the views asked for while the screen drew, which the frame edge reads off
/// the screen's thread (<see cref="ReadMetadataPageHandler"/>): each page once, however many frames show it loading
/// meanwhile (<see cref="MetadataGrid.Paged.TryAsk"/>).</summary>
public sealed class MetadataGridPages
{
    private readonly List<MetadataGridPageAsk> _asked = [];

    /// <summary>Whether pages were asked for that have not been started yet.</summary>
    public bool HasAsked => _asked.Count > 0;

    /// <summary>Asks for the page <paramref name="row"/> of <paramref name="grid"/> is on, unless it was read or asked
    /// for already.</summary>
    public void Ask(MetadataGrid.Paged grid, int row)
    {
        if (!grid.TryAsk(row))
        {
            return;
        }

        _asked.Add(new MetadataGridPageAsk { Grid = grid, Page = MetadataGrid.Paged.PageOf(row) });
    }

    /// <summary>The pages asked for since the last time, for the edge to start reading.</summary>
    public List<MetadataGridPageAsk> TakeAsked()
    {
        var asked = _asked.ToList();
        _asked.Clear();
        return asked;
    }
}
