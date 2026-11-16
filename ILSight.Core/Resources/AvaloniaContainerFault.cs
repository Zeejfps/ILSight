using System.Diagnostics;

namespace ILSight.Core.Resources;

/// <summary>Why an Avalonia asset container can't be listed, as found by ILSight's own reader
/// (<see cref="AvaloniaResources"/>): a cause the app words in each language.</summary>
public abstract record AvaloniaContainerFault
{
    private AvaloniaContainerFault()
    {
    }

    /// <summary>The resource name Avalonia embeds the container under, which its causes name it by.</summary>
    public const string ContainerName = "!AvaloniaResources";

    /// <summary>The index says a version no reader here knows (1, XML, and 2, binary, are read).</summary>
    public sealed record IndexVersionUnknown(int Version) : AvaloniaContainerFault;

    /// <summary>An XML index entry lacks <see cref="Field"/>, the element its path, offset or size is read from.</summary>
    public sealed record IndexEntryIncomplete(string Field) : AvaloniaContainerFault;

    /// <summary>The asset at <see cref="Path"/> is placed past the container's bytes.</summary>
    public sealed record EntryOutside(string Path) : AvaloniaContainerFault;

    public TResult Match<TResult>(Func<IndexVersionUnknown, TResult> indexVersionUnknown,
        Func<IndexEntryIncomplete, TResult> indexEntryIncomplete, Func<EntryOutside, TResult> entryOutside)
    {
        return this switch
        {
            IndexVersionUnknown v => indexVersionUnknown(v),
            IndexEntryIncomplete i => indexEntryIncomplete(i),
            EntryOutside o => entryOutside(o),
            _ => throw new UnreachableException($"AvaloniaContainerFault.Match does not handle {GetType().Name}."),
        };
    }
}
