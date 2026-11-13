using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.MetadataGridArea;

/// <summary>Reads a page of a paged grid's rows off the screen's thread, for <see cref="ReadMetadataPageHandler"/>: on
/// the thread pool, coming back on the UI thread with whatever it threw as <see cref="Outcome{T}.Failed"/>, except the
/// cancellation of the token the call was given, which it lets through.</summary>
public sealed class ReadMetadataPageHandlerAdapter : ReadMetadataPageHandler.IPorts
{
    public async Task<Outcome<ModuleRead<MetadataGridPage>>> ReadPageAsync(MetadataGridPageAsk ask, CancellationToken ct)
    {
        var read = await Background.Wait(Task.Run(() => MetadataGrids.ReadPage(ask.Grid, ask.Page), ct));
        ct.ThrowIfCancellationRequested();
        return read;
    }
}
