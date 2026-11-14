using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.MetadataGridArea;

/// <summary>Sorts and filters a grid's rows off the screen's thread, for <see cref="ArrangeMetadataGridHandler"/>: on
/// the thread pool, coming back on the UI thread. The core reads each cell's value with its failure in its place (sorted
/// as an empty cell, passing no filter): what else the arranging throws is a bug, which goes on to the workflow's edge,
/// as does the cancellation of the token the call was given.</summary>
public sealed class ArrangeMetadataGridHandlerAdapter : ArrangeMetadataGridHandler.IPorts
{
    public async Task<ModuleRead<MetadataGridView>> ArrangeAsync(MetadataGridViewAsk ask, CancellationToken ct)
    {
        var grid = ask.State.Grid;
        var arranged = await Background.Wait(Task.Run(() => MetadataGrids.ArrangeRows(grid, ask.SortColumn, ask.Descending, ask.Order, ask.Filters, ct), ct));
        ct.ThrowIfCancellationRequested();
        return arranged;
    }
}
