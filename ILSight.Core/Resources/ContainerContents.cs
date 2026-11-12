namespace ILSight.Core.Resources;

/// <summary>What a resource container holds besides its bytes: a <c>.resources</c> file its entries, read
/// once, and the grid of them; Avalonia's asset container, assets read when it is listed.</summary>
public abstract record ContainerContents
{
    private ContainerContents()
    {
    }

    public sealed record ResourcesFile(Lazy<List<KeyValuePair<string, object?>>> Entries, Lazy<Metadata.MetadataGrid> Grid) : ContainerContents;

    public sealed record AvaloniaAssets : ContainerContents;
}
