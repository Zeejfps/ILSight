using System.Globalization;
using System.Reflection;
using System.Text;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Util;
using ILSight.Core.Metadata;
using MetadataResource = ICSharpCode.Decompiler.Metadata.Resource;

namespace ILSight.Core.Resources;

/// <summary>A resource's bytes, which can be read to save them.</summary>
/// <param name="Description">What the resource is, as a resources folder lists it.</param>
/// <param name="FileName">The name a saved copy is offered under.</param>
/// <param name="ReadData">What a saved copy holds, read only when asked for.</param>
internal sealed record ResourceData(string Description, string FileName, Func<byte[]> ReadData);

internal static class ResourceNodes
{
    public static ResourceData DataOf(ResourceEntryInfo entry)
    {
        return new ResourceData(entry.Description, entry.FileName, entry.ReadData);
    }

    public static ResourceData DataOf(ResourceContainerInfo container)
    {
        return new ResourceData(container.Description, container.FileName, container.ReadData);
    }

    public static ResourceData DataOf(PackageFileInfo file)
    {
        return new ResourceData(file.Description, file.FileName, file.ReadData);
    }

    public static string Describe(MetadataResource resource)
    {
        var length = resource.TryGetLength();
        var size = length != null ? $", {length} bytes" : "";
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

    public static (bool IsPublic, NodeAccessibility Accessibility) Visibility(MetadataResource resource) =>
        (resource.Attributes & ManifestResourceAttributes.VisibilityMask) == ManifestResourceAttributes.Public
            ? (true, NodeAccessibility.Public)
            : (false, NodeAccessibility.Private);

    /// <summary>The entries in name order. The containers WPF generates key their entries by a
    /// URI-escaped path, which is unescaped as ILSpy does.</summary>
    public static List<KeyValuePair<string, object?>> ReadEntries(MetadataResource resource)
    {
        using var stream = new MemoryStream(Read(resource));
        IEnumerable<KeyValuePair<string, object?>> entries = new ResourcesFile(stream).ToList();
        if (WholeProjectDecompiler.IsWpfGeneratedResourceContainer(resource.Name))
            entries = entries.Select(static e => new KeyValuePair<string, object?>(Uri.UnescapeDataString(e.Key), e.Value));
        return entries.OrderBy(static e => e.Key, NaturalStringComparer.Instance).ToList();
    }

    /// <summary>The grid of the entries: each one's name, its value as text, and the type of value.</summary>
    public static GridSchema<(string Name, string Value, string Type)> Schema(LoadedModule assembly, Lazy<List<KeyValuePair<string, object?>>> entries) =>
        new GridSchema<(string Name, string Value, string Type)>("Resources", assembly.File, () => entries.Value.Select(static e => Row(e.Key, e.Value)))
            .Value("Name", static r => r.Name)
            .Value("Value", static r => r.Value, tooltip: static r => r.Value.Length > 80 || r.Value.Contains('\n') ? r.Value : null)
            .Value("Type", static r => r.Type);

    private static (string Name, string Value, string Type) Row(string name, object? value) =>
        value switch
        {
            null => (name, "null", ""),
            string text => (name, text, "System.String"),
            byte[] bytes => (name, $"{bytes.Length} bytes", "System.Byte[]"),
            MemoryStream stream => (name, $"{stream.Length} bytes", "System.IO.MemoryStream"),
            ResourceSerializedObject serialized => (name, "<serialized>", serialized.TypeName ?? ""),
            _ => (name, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", value.GetType().FullName ?? value.GetType().Name),
        };
}
