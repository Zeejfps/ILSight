using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.MetadataGridArea;

namespace ILSight.App.Tests;

/// <summary>Works out the rows asked for on the caller's thread, at once.</summary>
internal sealed class FakeArrangeMetadataGridHandlerPorts : ArrangeMetadataGridHandler.IPorts
{
    public Task<Outcome<ModuleRead<MetadataGridView>>> ArrangeAsync(MetadataGridViewAsk ask, CancellationToken ct)
    {
        var arranged = MetadataGrids.ArrangeRows(ask.State.Grid, ask.SortColumn, ask.Descending, ask.Order, ask.Filters, ct);
        return Task.FromResult<Outcome<ModuleRead<MetadataGridView>>>(new Outcome<ModuleRead<MetadataGridView>>.Ok(arranged));
    }
}
