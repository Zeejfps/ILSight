namespace ILSight.Core.ResourceArea;

/// <summary>What a resource container holds besides its bytes: a <c>.resources</c> file its entries, read
/// once, and the grid of them; Avalonia's asset container, assets read when it is listed.</summary>
public union ContainerContents(ContainerContents.ResourcesFile, ContainerContents.AvaloniaAssets)
{
    public sealed record ResourcesFile
    {
        public required Lazy<List<KeyValuePair<string, object?>>> Entries { get; init; }

        public required MetadataArea.LazyGrid Grid { get; init; }
    }

    public sealed record AvaloniaAssets;
}
