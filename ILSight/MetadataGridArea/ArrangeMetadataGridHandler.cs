using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.MetadataGridArea;

/// <summary>Sorts and filters the rows a grid's view asked for through <see cref="IPorts"/>, without holding up the
/// screen, and has the view show them when no newer view was asked for meanwhile.</summary>
public sealed class ArrangeMetadataGridHandler(ArrangeMetadataGridHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>The rows <paramref name="ask"/> asked for, worked out over its grid's cells' values without holding
        /// up the screen (<see cref="MetadataGrids.ArrangeRows"/>), each cell whose read failed sorted as empty and
        /// passing no filter; the cancellation of <paramref name="ct"/> is let through.</summary>
        Task<ModuleRead<MetadataGridView>> ArrangeAsync(MetadataGridViewAsk ask, CancellationToken ct);
    }

    /// <summary>Works out the rows <paramref name="ask"/> asked for and has its view show them, or, when the grid's
    /// module was closed before the work began, the rows as they were; dropped unsaid when a newer view was asked for
    /// meanwhile. Work that fails is settled by the edge that starts it, the view saying why
    /// (<see cref="MetadataGridState.FailArranging"/>).</summary>
    public async Task HandleAsync(MetadataGridViewAsk ask, CancellationToken ct)
    {
        var arranged = await ports.ArrangeAsync(ask, ct);
        arranged.Switch(read => ask.State.ShowArranged(ask, read.Value), _ => ask.State.StopArranging(ask.Key));
    }
}
