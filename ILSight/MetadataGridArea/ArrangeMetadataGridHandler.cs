using ILSight.Core.Metadata;

namespace ILSight.MetadataGridArea;

/// <summary>Sorts and filters the rows a grid's view asked for through <see cref="IPorts"/>, without holding up the
/// screen, and has the view show them when no newer view was asked for meanwhile.</summary>
public sealed class ArrangeMetadataGridHandler(ArrangeMetadataGridHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>The rows <paramref name="ask"/> asked for, worked out over its grid's cells' values without holding
        /// up the screen (<see cref="MetadataGrids.ArrangeRows"/>). Working them out that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>; the cancellation of <paramref name="ct"/> is let through.</summary>
        Task<Outcome<MetadataGridView>> ArrangeAsync(MetadataGridViewAsk ask, CancellationToken ct);
    }

    /// <summary>Works out the rows <paramref name="ask"/> asked for and has its view show them, or why they could not
    /// be worked out. Returns whether it does: false when a newer view was asked for meanwhile, which drops
    /// this one.</summary>
    public async Task<bool> HandleAsync(MetadataGridViewAsk ask, CancellationToken ct)
    {
        var arranged = await ports.ArrangeAsync(ask, ct);
        return arranged.Match(
            ok => MetadataGridWidget.ShowArranged(ask, ok.Value),
            failed => MetadataGridWidget.ShowArrangeFailed(ask, failed.Error));
    }
}
