using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.MetadataGridArea;

/// <summary>Sorts and filters a grid's rows off the screen's thread, for <see cref="ArrangeMetadataGridHandler"/>: on
/// the thread pool, coming back on the UI thread with whatever it threw as <see cref="Outcome{T}.Failed"/>, except the
/// cancellation of the token the call was given, which it lets through.</summary>
public sealed class ArrangeMetadataGridHandlerAdapter : ArrangeMetadataGridHandler.IPorts
{
    public async Task<Outcome<ModuleRead<MetadataGridView>>> ArrangeAsync(MetadataGridViewAsk ask, CancellationToken ct)
    {
        var grid = ask.State.Grid;
        var arranged = await Background.Wait(Task.Run(() => MetadataGrids.ArrangeRows(grid, ask.SortColumn, ask.Descending, ask.Order, ask.Filters, ct), ct));
        ct.ThrowIfCancellationRequested();
        return arranged;
    }
}
