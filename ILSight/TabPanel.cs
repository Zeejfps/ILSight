namespace ILSight;

/// <summary>The panel <see cref="Tab"/> is docked as, and how what it shows is viewed.</summary>
public sealed record TabPanel(DocumentTab Tab, string Panel, TabPanelView View);
