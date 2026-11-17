using System.Globalization;
using System.Reflection;
using System.Text;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Util;
using ILSight.Core.MetadataArea;
using MetadataResource = ICSharpCode.Decompiler.Metadata.Resource;

namespace ILSight.Core.ResourceArea;

internal static class ResourceNodes
{
    public static ResourceData CreateData(ResourceEntryInfo entry)
    {
        return new ResourceData
        {
            Description = entry.Description,
            FileName = entry.FileName,
            ReadData = () => ReadData(entry),
        };
    }

    public static ResourceData CreateData(ResourceContainerInfo container)
    {
        return new ResourceData
        {
            Description = container.Description,
            FileName = container.FileName,
            ReadData = () => ReadData(container),
        };
    }

    public static ResourceData CreateData(PackageFileInfo file)
    {
        return new ResourceData
        {
            Description = file.Description,
            FileName = file.File.Name,
            ReadData = () => PackageContents.ReadFileData(file),
        };
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
        return GridSchemas.AddColumns(GridSchemas.CreateGrid<(string Name, string Value, string Type)>("Resources", LoadedAssemblies.GetFile(assembly), () => entries.Value.Select(static e => Row(e.Key, e.Value))), columns =>
        {
            GridSchemas.AddValue(columns, "Name", static r => r.Name);
            GridSchemas.AddValue(columns, "Value", static r => r.Value, tooltip: static r => r.Value.Length > 80 || r.Value.Contains('\n') ? r.Value : null);
            GridSchemas.AddValue(columns, "Type", static r => r.Type);
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
        return entry.Source.Match(entry,
            static (_, manifest) => ResourceNodes.Read(manifest.Resource),
            static (_, bytes) => bytes.Data,
            static (info, baml) => Encoding.UTF8.GetBytes(DecompileBaml(info.Module, baml.Data)));
    }

    // Read in the unit reading the resource, which holds module; what the BAML's types resolve to is read under this
    // conversion's own reading. No catch here reads the resolver's mark, and none needs to (CF10): the one caller that
    // reaches a BAML entry's bytes, saving the resource, catches only the disk's failures, so what the conversion
    // throws reaches the edge and is recorded; a picture's read never reaches it (a BAML entry is never a picture).
    private static string DecompileBaml(LoadedModule module, byte[] data)
    {
        using var reading = LoadedAssemblies.BeginReading();
        return ResourceArea.Baml.DecompileToText(reading, module, data, new CallbackFailure(), CancellationToken.None);
    }

    internal static byte[] ReadData(ResourceContainerInfo container)
    {
        return ResourceNodes.Read(container.Resource);
    }

    /// <summary>The grid of a <c>.resources</c> file's entries, made the first time it is asked for with at least
    /// <paramref name="firstRows"/> rows read (<see cref="MetadataArea.LazyGrid"/>); null for an asset container, which has
    /// none.</summary>
    internal static Outcome<MetadataArea.MetadataGrid>? EnsureGrid(ResourceContainerInfo container, int firstRows, CancellationToken ct = default)
    {
        return container.Contents.Match<(int Rows, CancellationToken Ct), Outcome<MetadataArea.MetadataGrid>?>((firstRows, ct),
            static (s, file) => file.Grid.Ensure(s.Rows, s.Ct),
            static (_, _) => null);
    }

    internal static ResourceContainerInfo CreateResourcesFile(LoadedModule module, MetadataResource resource)
    {
        var entries = new Lazy<List<KeyValuePair<string, object?>>>(() => ResourceNodes.ReadEntries(resource));
        var grid = new MetadataArea.LazyGrid { Title = resource.Name, Module = module, File = LoadedAssemblies.GetFile(module), Schema = ResourceNodes.Schema(module, entries) };
        return new ResourceContainerInfo
        {
            Module = module,
            Resource = resource,
            Contents = new ContainerContents.ResourcesFile
            {
                Entries = entries,
                Grid = grid,
            },
        };
    }

    internal static ResourceContainerInfo CreateAvaloniaAssets(LoadedModule module, MetadataResource resource)
    {
        return new ResourceContainerInfo { Module = module, Resource = resource, Contents = new ContainerContents.AvaloniaAssets() };
    }

    /// <summary>The module's manifest resources, sorted by name.</summary>
    internal static BrowseNode[] ListResources(BrowseNode parent, LoadedModule module)
    {
        var resources = ListingReads.ValueOf(ReadResources(module));
        return resources
            .OrderBy(static r => r.Name, NaturalStringComparer.Instance)
            .Select(r => BrowseNodes.CreateListed(parent, CreateSubject(module, r.Resource, r.Name)))
            .ToArray();
    }

    // The module's manifest resources, each with its name.
    private static Outcome<(MetadataResource Resource, string Name)[]> ReadResources(LoadedModule module)
    {
        try
        {
            return new Outcome<(MetadataResource Resource, string Name)[]>.Ok(LoadedAssemblies.GetFile(module).Resources.Select(static r => (r, r.Name)).ToArray());
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<(MetadataResource Resource, string Name)[]>.Failed(e);
        }
    }

    private static NodeSubject CreateSubject(LoadedModule module, MetadataResource resource, string name)
    {
        if (AvaloniaResources.IsContainerName(name))
        {
            return new NodeSubject.ResourceContainer { Value = ResourceNodes.CreateAvaloniaAssets(module, resource) };
        }

        if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
        {
            return new NodeSubject.ResourceContainer { Value = ResourceNodes.CreateResourcesFile(module, resource) };
        }

        return new NodeSubject.ResourceEntry { Value = new ResourceEntryInfo { Module = module, Name = name, Source = new ResourceSource.Manifest(resource) } };
    }

    /// <summary>The entries of a container: a <c>.resources</c> file's byte and stream entries, its BAML
    /// among them, or Avalonia's assets, sorted by their paths.</summary>
    internal static BrowseNode[] ListEntries(BrowseNode parent, ResourceContainerInfo container)
    {
        return container.Contents.Match((parent, container),
            static (s, _) => ListingReads.ValueOf(ReadEntries(s.container))
                .Select(e => e.Value switch
                {
                    byte[] bytes => CreateEntry(s.parent, s.container.Module, e.Key, bytes),
                    MemoryStream stream => CreateEntry(s.parent, s.container.Module, e.Key, stream.ToArray()),
                    _ => (BrowseNode?)null,
                })
                .OfType<BrowseNode>()
                .ToArray(),
            static (s, _) =>
            {
                var entries = ListingReads.ValueOf(ReadAvaloniaEntries(s.container));
                return entries
                    .OrderBy(static e => e.Path, NaturalStringComparer.Instance)
                    .Select(e => BrowseNodes.CreateListed(s.parent, new NodeSubject.ResourceEntry { Value = new ResourceEntryInfo { Module = s.container.Module, Name = e.Path, Source = new ResourceSource.Entry(e.Data) } }))
                    .ToArray();
            });
    }

    // A .resources file's entries, each value as the library read it; read for the tree, the grid keeping its own.
    private static Outcome<KeyValuePair<string, object?>[]> ReadEntries(ResourceContainerInfo container)
    {
        try
        {
            return new Outcome<KeyValuePair<string, object?>[]>.Ok(ResourceNodes.ReadEntries(container.Resource).ToArray());
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<KeyValuePair<string, object?>[]>.Failed(e);
        }
    }

    // The Avalonia container's assets, each with its path and bytes.
    private static Outcome<(string Path, byte[] Data)[]> ReadAvaloniaEntries(ResourceContainerInfo container)
    {
        try
        {
            return new Outcome<(string Path, byte[] Data)[]>.Ok(AvaloniaResources.ReadEntries(ResourceNodes.ReadData(container)).ToArray());
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<(string Path, byte[] Data)[]>.Failed(e);
        }
    }

    private static BrowseNode CreateEntry(BrowseNode parent, LoadedModule module, string name, byte[] data)
    {
        ResourceSource source = Baml.IsBamlName(name) ? new ResourceSource.Baml(data) : new ResourceSource.Entry(data);
        return BrowseNodes.CreateListed(parent, new NodeSubject.ResourceEntry { Value = new ResourceEntryInfo { Module = module, Name = name, Source = source } });
    }
}
