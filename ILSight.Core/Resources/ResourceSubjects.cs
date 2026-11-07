using System.Text;
using MetadataResource = ICSharpCode.Decompiler.Metadata.Resource;

namespace ILSight.Core.Resources;

/// <summary>Where a resource's bytes come from: a manifest resource of the module, the bytes of an entry
/// of a container, or the compiled XAML of a <c>.resources</c> file's BAML entry, saved as the XAML.</summary>
public abstract record ResourceSource
{
    private ResourceSource()
    {
    }

    public sealed record Manifest(MetadataResource Resource) : ResourceSource;

    public sealed record Entry(byte[] Data) : ResourceSource;

    public sealed record Baml(byte[] Data) : ResourceSource;
}

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
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    public string FileName => Source switch
    {
        ResourceSource.Baml => Resources.Baml.XamlName(Path.GetFileName(Name)),
        ResourceSource.Manifest or ResourceSource.Entry => Path.GetFileName(Name),
        _ => throw new System.Diagnostics.UnreachableException(),
    };
}

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

/// <summary>A manifest resource that holds entries of its own: a <c>.resources</c> file, or the container
/// Avalonia embeds an app's assets in. It is its bytes, which it is saved as, and lists its entries.</summary>
public sealed class ResourceContainerInfo
{
    public required LoadedModule Module { get; init; }

    public required MetadataResource Resource { get; init; }

    public required ContainerContents Contents { get; init; }

    public string Description => ResourceNodes.Describe(Resource);

    public string FileName => Path.GetFileName(Resource.Name);
}
