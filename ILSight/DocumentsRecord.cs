using ZGF.Gui.Immediate.Widgets;

namespace ILSight;

/// <summary>The documents a list had open as the settings keep them: its tabs in the order opened, the
/// panels of the active tab and the preview tab, and how its document panels were arranged, a layout of
/// those panels alone.</summary>
public sealed record DocumentsRecord(List<DocumentRecord> Tabs, string? Active, string? Preview, DockLayout? Layout);
