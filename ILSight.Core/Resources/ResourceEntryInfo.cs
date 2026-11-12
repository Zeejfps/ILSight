namespace ILSight.Core.Resources;

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

    public string Description => Source.Match(Name,
        static (_, manifest) => ResourceNodes.Describe(manifest.Resource),
        static (name, entry) => $"{name} ({entry.Data.Length} bytes)",
        static (name, baml) => $"{name} ({baml.Data.Length} bytes)");

    public string FileName => Source.Match(Name,
        static (name, _) => Path.GetFileName(name),
        static (name, _) => Path.GetFileName(name),
        static (name, _) => Resources.Baml.XamlName(Path.GetFileName(name)));
}
