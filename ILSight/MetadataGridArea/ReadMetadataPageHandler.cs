using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.MetadataGridArea;

/// <summary>Reads a page of a paged grid's rows through <see cref="IPorts"/>, without holding up the screen, and has
/// the grid show it: its cells, or, when reading the page failed, each cell saying why.</summary>
public sealed class ReadMetadataPageHandler(ReadMetadataPageHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>The page <paramref name="ask"/> asked for, read through its grid's mapped file without holding up
        /// the screen (<see cref="MetadataGrids.ReadPage"/>). Reading it that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>; the cancellation of <paramref name="ct"/> is let through.</summary>
        Task<Outcome<ModuleRead<MetadataGridPage>>> ReadPageAsync(MetadataGridPageAsk ask, CancellationToken ct);
    }

    /// <summary>Reads the page <paramref name="ask"/> asked for and adds it to its grid, each cell's failure in its
    /// place when it could not be read; nothing new when the grid's module was closed before the read began.</summary>
    public async Task HandleAsync(MetadataGridPageAsk ask, CancellationToken ct)
    {
        var read = await ports.ReadPageAsync(ask, ct);
        var page = read.Match(
            static ok => ok.Value.Match<MetadataGridPage?>(static r => r.Value, static _ => null),
            failed => MetadataGrids.FailedPage(ask.Grid, ask.Page, failed.Error.Message));
        if (page is null)
        {
            return;
        }

        ask.Grid.AddPage(ask.Page, page);
    }
}
