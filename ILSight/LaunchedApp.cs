using ZGF.Desktop;
using ILSight.ILSightAppArea;

namespace ILSight;

/// <summary>The whole app the composition root builds for the launcher to open a window on, and where that window was
/// left; null for the default place.</summary>
internal sealed record LaunchedApp
{
    public required ILSightApp App { get; init; }

    public required WindowBounds? Window { get; init; }
}
