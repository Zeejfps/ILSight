using System.Diagnostics;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;

namespace ILSight;

/// <summary>Where the main window's panels go: the documents together right of the assemblies, and
/// the other views beside or below them. Methods that place a panel by the documents take the active
/// document's panel, whose group comes first.</summary>
public sealed class MainLayout
{
    public required DockLayout Dock { get; init; }
}
