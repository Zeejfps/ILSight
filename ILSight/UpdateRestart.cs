using Velopack;

namespace ILSight;

/// <summary>Given for one restart by <see cref="AppUpdates.StartRestart"/>: the update it hands over.</summary>
public sealed class UpdateRestart
{
    public required UpdateInfo Update { get; init; }
}
