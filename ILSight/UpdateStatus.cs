using Velopack;

namespace ILSight;

/// <summary>Where the app stands with its newer versions.</summary>
public abstract record UpdateStatus
{
    private UpdateStatus()
    {
    }

    /// <summary>Nothing has been looked for yet, or an automatic check found nothing to say.</summary>
    public sealed record Idle : UpdateStatus;

    public sealed record Checking : UpdateStatus;

    public sealed record Downloading(string Version) : UpdateStatus;

    /// <summary>A newer version is downloaded and takes over when the app restarts.</summary>
    public sealed record Ready(UpdateInfo Update) : UpdateStatus;

    public sealed record Restarting : UpdateStatus;

    /// <summary>The user asked, and this is the newest version.</summary>
    public sealed record UpToDate : UpdateStatus;

    /// <summary>What the user asked for could not be done; Velopack's log says why.</summary>
    public sealed record Failed : UpdateStatus;
}
