using static ILSight.Core.Metadata.GridSchemas;
using System.Globalization;
using System.Reflection;
using System.Text;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Util;
using ILSight.Core.Metadata;
using MetadataResource = ICSharpCode.Decompiler.Metadata.Resource;

namespace ILSight.Core.Resources;

internal static class ResourceNodes
{
    public static ResourceData CreateData(ResourceEntryInfo entry)
    {
        return new ResourceData(entry.Description, entry.FileName, () => ReadData(entry));
    }

    public static ResourceData CreateData(ResourceContainerInfo container)
    {
        return new ResourceData(container.Description, container.FileName, () => ReadData(container));
    }

    public static ResourceData CreateData(PackageFileInfo file)
    {
        return new ResourceData(file.Description, file.FileName, () => PackageContents.ReadFileData(file));
    }

    public static string Describe(MetadataResource resource)
    {
        var length = resource.TryGetLength();
        var size = length is not null ? $", {length} bytes" : "";
        return $"{resource.Name} ({resource.ResourceType}{size})";
    }

    public static byte[] Read(MetadataResource resource)
    {
        using var stream = resource.TryOpenStream() ?? throw new IOException($"{resource.Name} cannot be read.");
        stream.Position = 0;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    public static (bool IsPublic, NodeAccessibility Accessibility) Visibility(MetadataResource resource)
    {
        return (resource.Attributes & ManifestResourceAttributes.VisibilityMask) == ManifestResourceAttributes.Public
            ? (true, NodeAccessibility.Public)
            : (false, NodeAccessibility.Private);
    }

    /// <summary>The entries in name order. The containers WPF generates key their entries by a
    /// URI-escaped path, which is unescaped as ILSpy does.</summary>
    public static List<KeyValuePair<string, object?>> ReadEntries(MetadataResource resource)
    {
        using var stream = new MemoryStream(Read(resource));
        IEnumerable<KeyValuePair<string, object?>> entries = new ResourcesFile(stream).ToList();
        if (WholeProjectDecompiler.IsWpfGeneratedResourceContainer(resource.Name))
        {
            entries = entries.Select(static e => new KeyValuePair<string, object?>(Uri.UnescapeDataString(e.Key), e.Value));
        }

        return entries.OrderBy(static e => e.Key, NaturalStringComparer.Instance).ToList();
    }

    /// <summary>The grid of the entries: each one's name, its value as text, and the type of value.</summary>
    public static GridSchema<(string Name, string Value, string Type)> Schema(LoadedModule assembly, Lazy<List<KeyValuePair<string, object?>>> entries)
    {
        return AddColumns(CreateGrid<(string Name, string Value, string Type)>("Resources", assembly.File, () => entries.Value.Select(static e => Row(e.Key, e.Value))), columns =>
        {
            AddValue(columns, "Name", static r => r.Name);
            AddValue(columns, "Value", static r => r.Value, tooltip: static r => r.Value.Length > 80 || r.Value.Contains('\n') ? r.Value : null);
            AddValue(columns, "Type", static r => r.Type);
        });
    }

    private static (string Name, string Value, string Type) Row(string name, object? value)
    {
        return value switch
        {
            null => (name, "null", ""),
            string text => (name, text, "System.String"),
            byte[] bytes => (name, $"{bytes.Length} bytes", "System.Byte[]"),
            MemoryStream stream => (name, $"{stream.Length} bytes", "System.IO.MemoryStream"),
            ResourceSerializedObject serialized => (name, "<serialized>", serialized.TypeName ?? ""),
            _ => (name, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", value.GetType().FullName ?? value.GetType().Name),
        };
    }

    internal static byte[] ReadData(ResourceEntryInfo entry)
    {
        return entry.Source switch
        {
            ResourceSource.Manifest manifest => ResourceNodes.Read(manifest.Resource),
            ResourceSource.Entry bytes => bytes.Data,
            ResourceSource.Baml baml => Encoding.UTF8.GetBytes(Resources.Baml.DecompileToText(entry.Module, baml.Data, CancellationToken.None)),
            _ => throw new System.Diagnostics.UnreachableException(),
        };
    }

    internal static byte[] ReadData(ResourceContainerInfo container)
    {
        return ResourceNodes.Read(container.Resource);
    }

    /// <summary>The grid of a <c>.resources</c> file's entries, made the first time it is asked for; null for an
    /// asset container, which has none.</summary>
    internal static Metadata.MetadataGrid? EnsureGrid(ResourceContainerInfo container)
    {
        return container.Contents switch
        {
            ContainerContents.ResourcesFile file => file.Grid.Value,
            ContainerContents.AvaloniaAssets => null,
            _ => throw new System.Diagnostics.UnreachableException(),
        };
    }

    internal static ResourceContainerInfo CreateResourcesFile(LoadedModule module, MetadataResource resource)
    {
        var entries = new Lazy<List<KeyValuePair<string, object?>>>(() => ResourceNodes.ReadEntries(resource));
        var grid = new Lazy<Metadata.MetadataGrid>(() => Metadata.MetadataGrids.CreateGrid(resource.Name, module, module.File, ResourceNodes.Schema(module, entries)));
        return new ResourceContainerInfo { Module = module, Resource = resource, Contents = new ContainerContents.ResourcesFile(entries, grid) };
    }

    internal static ResourceContainerInfo CreateAvaloniaAssets(LoadedModule module, MetadataResource resource)
    {
        return new ResourceContainerInfo { Module = module, Resource = resource, Contents = new ContainerContents.AvaloniaAssets() };
    }

    /// <summary>The module's manifest resources, sorted by name.</summary>
    internal static BrowseNode[] ListResources(BrowseNode parent, LoadedModule module)
    {
        return module.File.Resources
            .OrderBy(r => r.Name, NaturalStringComparer.Instance)
            .Select(r => BrowseNodes.Create(parent, CreateSubject(module, r)))
            .ToArray();
    }

    /// <summary>What a manifest resource stands for: the entries of a <c>.resources</c> file or of
    /// Avalonia's asset container, or the resource itself.</summary>
    private static NodeSubject CreateSubject(LoadedModule module, MetadataResource resource)
    {
        if (AvaloniaResources.IsContainerName(resource.Name))
        {
            return new NodeSubject.ResourceContainer { Value = ResourceNodes.CreateAvaloniaAssets(module, resource) };
        }

        if (resource.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
        {
            return new NodeSubject.ResourceContainer { Value = ResourceNodes.CreateResourcesFile(module, resource) };
        }

        return new NodeSubject.ResourceEntry { Value = new ResourceEntryInfo { Module = module, Name = resource.Name, Source = new ResourceSource.Manifest(resource) } };
    }

    /// <summary>The entries of a container: a <c>.resources</c> file's byte and stream entries, its BAML
    /// among them, or Avalonia's assets, sorted by their paths.</summary>
    internal static BrowseNode[] ListEntries(BrowseNode parent, ResourceContainerInfo container)
    {
        return container.Contents switch
        {
            ContainerContents.ResourcesFile file => file.Entries.Value
                .Select(e => e.Value switch
                {
                    byte[] bytes => CreateEntry(parent, container.Module, e.Key, bytes),
                    MemoryStream stream => CreateEntry(parent, container.Module, e.Key, stream.ToArray()),
                    _ => (BrowseNode?)null,
                })
                .OfType<BrowseNode>()
                .ToArray(),
            ContainerContents.AvaloniaAssets => AvaloniaResources.ReadEntries(ResourceNodes.ReadData(container))
                .OrderBy(e => e.Path, NaturalStringComparer.Instance)
                .Select(e => BrowseNodes.Create(parent, new NodeSubject.ResourceEntry { Value = new ResourceEntryInfo { Module = container.Module, Name = e.Path, Source = new ResourceSource.Entry(e.Data) } }))
                .ToArray(),
            _ => throw new System.Diagnostics.UnreachableException(),
        };
    }

    private static BrowseNode CreateEntry(BrowseNode parent, LoadedModule module, string name, byte[] data)
    {
        ResourceSource source = Baml.IsBamlName(name) ? new ResourceSource.Baml(data) : new ResourceSource.Entry(data);
        return BrowseNodes.Create(parent, new NodeSubject.ResourceEntry { Value = new ResourceEntryInfo { Module = module, Name = name, Source = source } });
    }
}
