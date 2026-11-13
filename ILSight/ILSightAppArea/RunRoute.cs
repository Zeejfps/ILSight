namespace ILSight.ILSightAppArea;

/// <summary>Where a run's arguments go (<see cref="RunRouting"/>).</summary>
public enum RunRoute
{
    /// <summary>This run took the role: it opens the window and receives later runs' arguments.</summary>
    Receives,

    /// <summary>The running app took the arguments: this run ends.</summary>
    HandedOver,

    /// <summary>This run opens its own window and takes the role when it comes free.</summary>
    StandsBy,
}
