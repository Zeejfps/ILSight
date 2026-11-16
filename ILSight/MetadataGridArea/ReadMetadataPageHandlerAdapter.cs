using ILSight.Core;
using ILSight.Core.MetadataArea;

namespace ILSight.MetadataGridArea;

/// <summary>Reads a page of a paged grid's rows off the screen's thread, for <see cref="ReadMetadataPageHandler"/>: on
/// the thread pool, coming back on the UI thread with a page the library failed on as <see cref="Outcome{T}.Failed"/>,
/// as the core answers it; the cancellation of the token the call was given is let through.</summary>
public sealed class ReadMetadataPageHandlerAdapter : ReadMetadataPageHandler.IPorts
{
    public async Task<Outcome<ModuleRead<MetadataGridPage>>> ReadPageAsync(MetadataGridPageAsk ask, CancellationToken ct)
    {
        var read = await Background.Wait(Task.Run(() => MetadataGrids.ReadPage(ask.Grid, ask.Page), ct));
        ct.ThrowIfCancellationRequested();
        return read;
    }
}
