using ILSight.Core;
using ILSight.UserArea;

namespace ILSight.App.Tests;

/// <summary>The adapter over a real folder: what it does with the watch as the app shuts down.</summary>
public sealed class UserThemesAdapterTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public async Task WaitForChangeAsync_ShutdownWhileWaiting_EndsCancelledAndStopsTheWatch()
    {
        var folder = _temp.PathOf("themes");
        Directory.CreateDirectory(folder);
        var adapter = new UserColorThemesAdapter(new ErrorLog());
        Assert.True(adapter.TryStartWatching(folder, out _));
        using var shutdown = new CancellationTokenSource();
        var waiting = StartWithoutContextAsync(() => adapter.WaitForChangeAsync(shutdown.Token));

        await shutdown.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WithinPatienceAsync());
        var after = await StartWithoutContextAsync(() => adapter.WaitForChangeAsync(CancellationToken.None)).WithinPatienceAsync();
        Assert.Equal(ThemeFolderEventKind.Stopped, Assert.IsType<Outcome<ThemeFolderEvent>.Ok>(after).Value.Kind);
    }

    // Starts the wait with no synchronization context, as the app's adapter is awaited where there is no UI thread, so
    // it continues on the thread pool: not on the test framework's context, whose few threads the other tests' frames
    // block under load. The test then waits for the wait's end, bounded only by the suite's patience for work elsewhere.
    private static Task<T> StartWithoutContextAsync<T>(Func<Task<T>> start)
    {
        var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return start();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    [Fact]
    public void ReadThemeFile_FileMissing_SaysWhyItCannotBeRead()
    {
        var adapter = new UserColorThemesAdapter(new ErrorLog());
        var path = _temp.PathOf("themes/gone.json");

        var read = adapter.ReadThemeFile(path);

        Assert.IsType<DirectoryNotFoundException>(Assert.IsType<Outcome<byte[]>.Failed>(read).Error);
    }
}
