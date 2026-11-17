using Velopack;

namespace ILSight;

/// <summary>Where the app stands with its newer versions.</summary>
public union UpdateStatus(UpdateStatus.Idle, UpdateStatus.Checking, UpdateStatus.Downloading, UpdateStatus.Ready,
    UpdateStatus.Restarting, UpdateStatus.UpToDate, UpdateStatus.Failed)
{
    /// <summary>Nothing has been looked for yet, or an automatic check found nothing to say.</summary>
    public sealed record Idle;

    public sealed record Checking;

    public sealed record Downloading(string Version);

    /// <summary>A newer version is downloaded and takes over when the app restarts.</summary>
    public sealed record Ready(UpdateInfo Update);

    public sealed record Restarting;

    /// <summary>The user asked, and this is the newest version.</summary>
    public sealed record UpToDate;

    /// <summary>What the user asked for could not be done; Velopack's log says why.</summary>
    public sealed record Failed;
}
