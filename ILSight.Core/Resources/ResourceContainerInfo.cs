using MetadataResource = ICSharpCode.Decompiler.Metadata.Resource;

namespace ILSight.Core.Resources;

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
