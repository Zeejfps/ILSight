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
