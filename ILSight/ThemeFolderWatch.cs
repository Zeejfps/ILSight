using System.ComponentModel;

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

/// <summary>What <see cref="ThemeFolderWatch.NextChange"/> completes with; <see cref="Reason"/> says why
/// a watch was <see cref="ThemeFolderEventKind.Lost"/>.</summary>
public readonly record struct ThemeFolderEvent(ThemeFolderEventKind Kind, string Reason = "");

/// <summary>Watches a folder of the user's themes for changes to the files of the kinds it is made for,
/// such as the theme files of the themes folder. <see cref="NextChange"/> completes once those files
/// changed and then stayed quiet for a moment, so the several writes of one save count once, or once the
/// watch is lost. What happens while no one waits stays pending until the next <see cref="NextChange"/>
/// takes it. The file system tells of changes on other threads; this only completes a task there, for
/// the UI thread to await.</summary>
public sealed class ThemeFolderWatch
{
    public const string ThemeFilter = "*" + ThemeFile.Extension;

    public readonly Lock Gate = new();
    public FileSystemWatcher? Watcher;
    public FileSystemWatcher? RemovalWatcher;

    /// <summary>The files watched, such as <c>*.json</c>.</summary>
    public required IReadOnlyList<string> Filters { get; init; }

    /// <summary>Whether the folders inside the folder are watched too.</summary>
    public required bool IncludesSubfolders { get; init; }

    public Timer? Quiet;
    public TaskCompletionSource<ThemeFolderEvent>? Waiter;
    public bool IsChangePending;
    public string? PendingLoss;
}
