namespace ILSight;

/// <summary>The panel <see cref="Tab"/> is docked as, and how what it shows is viewed.</summary>
public sealed record TabPanel
{
    public required DocumentTab Tab { get; init; }

    public required string Panel { get; init; }

    public required TabPanelView View { get; init; }
}
