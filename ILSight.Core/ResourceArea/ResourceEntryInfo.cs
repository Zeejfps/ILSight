namespace ILSight.Core.ResourceArea;

/// <summary>A resource shown as its bytes: text, a picture, a hex dump, or for BAML the XAML it was
/// compiled from; saved as its bytes, or as that XAML.</summary>
public sealed class ResourceEntryInfo
{
    public required LoadedModule Module { get; init; }

    /// <summary>The resource's name, or the entry's path in its container.</summary>
    public required string Name { get; init; }

    public required ResourceSource Source { get; init; }

    /// <summary>Whether it is shown as a picture: named as a picture is, and no BAML.</summary>
    public bool IsImage => Source is not ResourceSource.Baml && ResourceImages.IsImageName(Name);

    public string Description => Source switch
    {
        ResourceSource.Manifest manifest => ResourceNodes.Describe(manifest.Resource),
        ResourceSource.Entry entry => $"{Name} ({entry.Data.Length} bytes)",
        ResourceSource.Baml baml => $"{Name} ({baml.Data.Length} bytes)",
    };

    public string FileName => Source switch
    {
        ResourceSource.Manifest or ResourceSource.Entry => Path.GetFileName(Name),
        ResourceSource.Baml => ResourceArea.Baml.XamlName(Path.GetFileName(Name)),
    };
}
