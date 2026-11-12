namespace ILSight;

/// <summary>What a try at focusing another tab saw: the layout's change count and how many tabs were open.
/// A try is due once each time this changes, so one that found none is not repeated every frame.</summary>
public readonly record struct TabFocusTry
{
    public required int LayoutChanges { get; init; }

    public required int TabCount { get; init; }
}
