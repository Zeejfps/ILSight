namespace ILSight.Core.ResourceArea;

/// <summary>Bad data ILSight's own reader of an Avalonia asset container found: <see cref="Fault"/> is the cause the
/// app words, the message its English diagnostic, for the record.</summary>
public sealed class AvaloniaContainerException(AvaloniaContainerFault fault) : Exception(Diagnose(fault))
{
    public AvaloniaContainerFault Fault { get; } = fault;

    private static string Diagnose(AvaloniaContainerFault fault)
    {
        return fault switch
        {
            AvaloniaContainerFault.IndexVersionUnknown v => $"{AvaloniaContainerFault.ContainerName} has an index of unknown version {v.Version}.",
            AvaloniaContainerFault.IndexEntryIncomplete i => $"{AvaloniaContainerFault.ContainerName} has an index entry without {i.Field}.",
            AvaloniaContainerFault.EntryOutside o => $"{o.Path} lies outside {AvaloniaContainerFault.ContainerName}.",
        };
    }
}
