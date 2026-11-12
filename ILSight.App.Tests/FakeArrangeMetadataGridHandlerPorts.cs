using ILSight.Core.Metadata;
using ILSight.MetadataGridArea;

namespace ILSight.App.Tests;

/// <summary>Works out the rows asked for on the caller's thread, at once.</summary>
internal sealed class FakeArrangeMetadataGridHandlerPorts : ArrangeMetadataGridHandler.IPorts
{
    public Task<Outcome<MetadataGridView>> ArrangeAsync(MetadataGridViewAsk ask, CancellationToken ct)
    {
        var view = MetadataGrids.ArrangeRows(ask.State.Grid, ask.SortColumn, ask.Descending, ask.Order, ask.Filters, ct);
        return Task.FromResult<Outcome<MetadataGridView>>(new Outcome<MetadataGridView>.Ok(view));
    }
}
