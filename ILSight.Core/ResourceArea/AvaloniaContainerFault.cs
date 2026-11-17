namespace ILSight.Core.ResourceArea;

/// <summary>Why an Avalonia asset container can't be listed, as found by ILSight's own reader
/// (<see cref="AvaloniaResources"/>): a cause the app words in each language.</summary>
public union AvaloniaContainerFault(AvaloniaContainerFault.IndexVersionUnknown, AvaloniaContainerFault.IndexEntryIncomplete,
    AvaloniaContainerFault.EntryOutside)
{
    /// <summary>The resource name Avalonia embeds the container under, which its causes name it by.</summary>
    public const string ContainerName = "!AvaloniaResources";

    /// <summary>The index says a version no reader here knows (1, XML, and 2, binary, are read).</summary>
    public sealed record IndexVersionUnknown(int Version);

    /// <summary>An XML index entry lacks <see cref="Field"/>, the element its path, offset or size is read from.</summary>
    public sealed record IndexEntryIncomplete(string Field);

    /// <summary>The asset at <see cref="Path"/> is placed past the container's bytes.</summary>
    public sealed record EntryOutside(string Path);
}
