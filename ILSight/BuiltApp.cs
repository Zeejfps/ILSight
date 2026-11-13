using ILSight.ILSightAppArea;

namespace ILSight;

/// <summary>The whole app the composition root builds to start: the app state and the screen over it, and the app
/// that runs them in a window.</summary>
internal sealed record BuiltApp
{
    public required RestoredApp Restored { get; init; }

    public required ILSightApp App { get; init; }
}
