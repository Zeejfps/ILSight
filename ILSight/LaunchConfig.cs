using ILSight.Core;
using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>How this run starts, which means nothing to an app already running: the settings it uses, how
/// it draws, and whether it opens a window of its own rather than hand its command to the one already open
/// with the same settings.</summary>
public sealed record LaunchConfig
{
    public required FullFilePath SettingsPath { get; init; }

    public required RenderBackendKind RenderBackend { get; init; }

    public required bool NewInstance { get; init; }
}
