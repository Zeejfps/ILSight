using ILSight.Core.Metadata;
using ILSight.MetadataGridArea;

namespace ILSight.App.Tests;

/// <summary>Fails every page's read at once, as a disk gone would.</summary>
internal sealed class FailingReadMetadataPageHandlerPorts : ReadMetadataPageHandler.IPorts
{
    public const string Why = "The disk is gone.";

    public Task<Outcome<MetadataGridPage>> ReadPageAsync(MetadataGridPageAsk ask, CancellationToken ct)
    {
        return Task.FromResult<Outcome<MetadataGridPage>>(new Outcome<MetadataGridPage>.Failed(new IOException(Why)));
    }
}
