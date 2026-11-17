using MetadataResource = ICSharpCode.Decompiler.Metadata.Resource;

namespace ILSight.Core.ResourceArea;

/// <summary>Where a resource's bytes come from: a manifest resource of the module, the bytes of an entry
/// of a container, or the compiled XAML of a <c>.resources</c> file's BAML entry, saved as the XAML.</summary>
public union ResourceSource(ResourceSource.Manifest, ResourceSource.Entry, ResourceSource.Baml)
{
    public sealed record Manifest(MetadataResource Resource);

    public sealed record Entry(byte[] Data);

    public sealed record Baml(byte[] Data);
}
