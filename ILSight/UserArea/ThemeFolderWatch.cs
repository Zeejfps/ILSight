using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ILSight.Core;

namespace ILSight.UserArea;

/// <summary>Watches a folder of the user's themes for changes to the files of the kinds it is made for,
/// such as the theme files of the themes folder. <see cref="NextChangeAsync"/> completes once those files
/// changed and then stayed quiet for a moment, so the several writes of one save count once, or once the
/// watch is lost. What happens while no one waits stays pending until the next <see cref="NextChangeAsync"/>
/// takes it. The file system tells of changes on other threads; this only completes a task there, for
/// the UI thread to await.</summary>
public sealed class ThemeFolderWatch
{
    private const int QuietMilliseconds = 200;
    private const string RemovedReason = "The folder was removed or renamed.";

    private static readonly Task<ThemeFolderEvent> Changed = Task.FromResult(new ThemeFolderEvent
    {
        Kind = ThemeFolderEventKind.Changed,
    });
    private static readonly Task<ThemeFolderEvent> Stopped = Task.FromResult(new ThemeFolderEvent
    {
        Kind = ThemeFolderEventKind.Stopped,
    });

    // Shared by the file system's threads and the UI thread.
    private readonly Guarded<Shared> _held = new(new Shared());

    /// <summary>The files watched, such as <c>*.json</c>.</summary>
    public required IReadOnlyList<string> Filters { get; init; }

    /// <summary>Whether the folders inside the folder are watched too.</summary>
    public required bool IncludesSubfolders { get; init; }

    /// <summary>Starts watching <paramref name="folder"/> in place of the folder watched before. False, with
    /// why the system cannot watch it in <paramref name="reason"/>, such as a folder gone or a limit on
    /// watches reached, and then watches nothing.</summary>
    public bool TryStart(string folder, [NotNullWhen(false)] out string? reason)
    {
        Stop();
        try
        {
            WatchFolder(folder);
            reason = null;
            return true;
        }
        catch (IOException e)
        {
            reason = StopFailed(folder, e);
            return false;
        }
        catch (ArgumentException e)
        {
            reason = StopFailed(folder, e);
            return false;
        }
        catch (UnauthorizedAccessException e)
        {
            reason = StopFailed(folder, e);
            return false;
        }
        catch (PlatformNotSupportedException e)
        {
            reason = StopFailed(folder, e);
            return false;
        }
        catch (Win32Exception e)
        {
            reason = StopFailed(folder, e);
            return false;
        }
    }

    private void WatchFolder(string folder)
    {
        var watcher = new FileSystemWatcher(folder);
        AddFilters(watcher);
        watcher.IncludeSubdirectories = IncludesSubfolders;
        var quiet = new Timer(OnQuiet);
        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Deleted += OnChanged;
        watcher.Renamed += OnChanged;
        watcher.Error += OnError;
        var removalWatcher = WatchRemoval(watcher);
        using (var held = _held.Enter())
        {
            var shared = held.Value;
            shared.Quiet = quiet;
            shared.Watcher = watcher;
            shared.RemovalWatcher = removalWatcher;
        }
        watcher.EnableRaisingEvents = true;
        if (removalWatcher is not null)
        {
            removalWatcher.EnableRaisingEvents = true;
        }
    }

    private void AddFilters(FileSystemWatcher watcher)
    {
        foreach (var filter in Filters)
        {
            watcher.Filters.Add(filter);
        }
    }

    // Watches the parent folder to lose the watch once the folder is removed or renamed away: macOS tells the
    // folder's own watcher nothing of either.
    private FileSystemWatcher? WatchRemoval(FileSystemWatcher watcher)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(watcher.Path));
        var parent = Path.GetDirectoryName(folder);
        // A folder at the root of a drive cannot be removed.
        if (parent is null)
        {
            return null;
        }

        var removalWatcher = new FileSystemWatcher(parent, Path.GetFileName(folder))
        {
            NotifyFilter = NotifyFilters.DirectoryName,
        };
        removalWatcher.Deleted += (_, _) => LoseFolder(watcher);
        removalWatcher.Renamed += (_, e) =>
        {
            if (e.OldFullPath == folder)
            {
                LoseFolder(watcher);
            }
        };
        return removalWatcher;
    }

    private string StopFailed(string folder, Exception e)
    {
        Stop();
        return DescribeWatchFailure(folder, e.Message);
    }

    private static string DescribeWatchFailure(string folder, string message)
    {
        return $"{folder}{Environment.NewLine}{message}";
    }

    /// <summary>Completes once the watched files changed or the watch was lost, at once when either is
    /// pending, or as stopped when watching stops first or is not running. Ends as cancelled when
    /// <paramref name="ct"/> fires, leaving the watch as it is.</summary>
    public Task<ThemeFolderEvent> NextChangeAsync(CancellationToken ct)
    {
        using var held = _held.Enter();
        var shared = held.Value;
        var loss = shared.PendingLoss;
        if (loss is not null)
        {
            shared.PendingLoss = null;
            return Task.FromResult(new ThemeFolderEvent { Kind = ThemeFolderEventKind.Lost, Reason = loss });
        }

        if (shared.Watcher is null)
        {
            return Stopped;
        }

        if (shared.IsChangePending)
        {
            shared.IsChangePending = false;
            return Changed;
        }

        shared.Waiter ??= new TaskCompletionSource<ThemeFolderEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        return shared.Waiter.Task.WaitAsync(ct);
    }

    /// <summary>Stops watching, ending the wait as stopped and dropping what is pending.</summary>
    public void Stop()
    {
        var waiter = CloseWatch(null);
        waiter?.TrySetResult(new ThemeFolderEvent { Kind = ThemeFolderEventKind.Stopped });
    }

    private TaskCompletionSource<ThemeFolderEvent>? CloseWatch(string? loss, FileSystemWatcher? lost = null)
    {
        FileSystemWatcher? watcher;
        FileSystemWatcher? removalWatcher;
        Timer? quiet;
        TaskCompletionSource<ThemeFolderEvent>? waiter;
        using (var held = _held.Enter())
        {
            var shared = held.Value;
            if (IsWatchReplaced(shared, lost))
            {
                return null;
            }

            watcher = shared.Watcher;
            removalWatcher = shared.RemovalWatcher;
            quiet = shared.Quiet;
            waiter = shared.Waiter;
            shared.Watcher = null;
            shared.RemovalWatcher = null;
            shared.Quiet = null;
            shared.Waiter = null;
            shared.IsChangePending = false;
            shared.PendingLoss = waiter is null ? loss : null;
        }
        watcher?.Dispose();
        removalWatcher?.Dispose();
        quiet?.Dispose();
        return waiter;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        WaitForQuiet();
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        var error = e.GetException();
        // An overflow only dropped changes the system could not keep up with: the folder is read again.
        if (error is InternalBufferOverflowException)
        {
            WaitForQuiet();
            return;
        }
        if (sender is not FileSystemWatcher watcher)
        {
            throw new UnreachableException("Only a FileSystemWatcher raises its own Error event.");
        }

        // On any other error the system has stopped telling, so the watch is lost.
        LoseWatch(watcher, DescribeWatchFailure(watcher.Path, error.Message));
    }

    private void LoseFolder(FileSystemWatcher watcher)
    {
        LoseWatch(watcher, DescribeWatchFailure(watcher.Path, RemovedReason));
    }

    private void LoseWatch(FileSystemWatcher watcher, string reason)
    {
        var waiter = CloseWatch(reason, watcher);
        waiter?.TrySetResult(new ThemeFolderEvent { Kind = ThemeFolderEventKind.Lost, Reason = reason });
    }

    private static bool IsWatchReplaced(Shared shared, FileSystemWatcher? lost)
    {
        if (lost is null)
        {
            return false;
        }

        return !ReferenceEquals(lost, shared.Watcher);
    }

    private void WaitForQuiet()
    {
        using var held = _held.Enter();
        held.Value.Quiet?.Change(QuietMilliseconds, Timeout.Infinite);
    }

    private void OnQuiet(object? state)
    {
        TaskCompletionSource<ThemeFolderEvent>? waiter;
        using (var held = _held.Enter())
        {
            var shared = held.Value;
            if (shared.Watcher is null)
            {
                return;
            }

            waiter = shared.Waiter;
            shared.Waiter = null;
            if (waiter is null)
            {
                shared.IsChangePending = true;
            }
        }
        waiter?.TrySetResult(new ThemeFolderEvent { Kind = ThemeFolderEventKind.Changed });
    }

    private sealed class Shared
    {
        public FileSystemWatcher? Watcher;
        public FileSystemWatcher? RemovalWatcher;
        public Timer? Quiet;
        public TaskCompletionSource<ThemeFolderEvent>? Waiter;
        public bool IsChangePending;
        public string? PendingLoss;
    }
}
