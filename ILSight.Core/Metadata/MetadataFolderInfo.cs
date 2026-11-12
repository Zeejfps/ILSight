using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

/// <summary>The raw CLI metadata of a file of <see cref="Module"/>: its own, or its debug metadata, under
/// a folder titled <see cref="Title"/>.</summary>
public sealed class MetadataFolderInfo
{
    public required LoadedModule Module { get; init; }

    public required MetadataFile File { get; init; }

    public required string Title { get; init; }
}
