namespace ILSight;

/// <summary>How a wait on the themes folder ended.</summary>
public enum ThemeFolderEventKind
{
    /// <summary>Watching stopped, or was not running.</summary>
    Stopped,

    /// <summary>The watched files changed.</summary>
    Changed,

    /// <summary>The system stopped telling of changes, as when the folder was removed; nothing is watched since.</summary>
    Lost,
}
