using ILSight.UserArea;

namespace ILSight.App.Tests;

public sealed class ThemeFolderWatchTests : IDisposable
{
    private static readonly TimeSpan Settled = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly TempDir _temp = new();
    private readonly ThemeFolderWatch _watch = new ThemeFolderWatch { Filters = ["*.json"], IncludesSubfolders = false };

    public void Dispose()
    {
        _watch.Stop();
        _temp.Dispose();
    }

    private string Folder => Path.GetDirectoryName(_temp.Write("themes/.keep", "")) ?? throw new InvalidOperationException("A file written to the temp folder is in a folder.");

    [Fact]
    public async Task A_change_that_settles_while_no_one_waits_is_kept_for_the_next_wait()
    {
        Assert.True(_watch.TryStart(Folder, out _));
        var first = _watch.NextChangeAsync(CancellationToken.None);
        _temp.Write("themes/a.json", "{}");
        Assert.Equal(ThemeFolderEventKind.Changed, (await first.WaitAsync(Patience)).Kind);

        _temp.Write("themes/b.json", "{}");
        await Task.Delay(Settled);
        var second = _watch.NextChangeAsync(CancellationToken.None);

        Assert.Equal(ThemeFolderEventKind.Changed, (await second.WaitAsync(Patience)).Kind);
    }

    [Fact]
    public async Task A_change_is_taken_once()
    {
        Assert.True(_watch.TryStart(Folder, out _));
        _temp.Write("themes/a.json", "{}");
        await Task.Delay(Settled);
        Assert.Equal(ThemeFolderEventKind.Changed, (await _watch.NextChangeAsync(CancellationToken.None).WaitAsync(Patience)).Kind);

        var next = _watch.NextChangeAsync(CancellationToken.None);
        var first = await Task.WhenAny(next, Task.Delay(Settled));

        Assert.NotSame(next, first);
    }

    [Fact]
    public async Task Stopping_ends_the_wait_and_drops_a_pending_change()
    {
        Assert.True(_watch.TryStart(Folder, out _));
        var waiting = _watch.NextChangeAsync(CancellationToken.None);

        _watch.Stop();

        Assert.Equal(ThemeFolderEventKind.Stopped, (await waiting.WaitAsync(Patience)).Kind);
        Assert.Equal(ThemeFolderEventKind.Stopped, (await _watch.NextChangeAsync(CancellationToken.None)).Kind);
    }

    [Fact]
    public async Task A_folder_that_cannot_be_watched_is_told_and_nothing_is_watched()
    {
        var missing = _temp.PathOf("missing");

        var isWatched = _watch.TryStart(missing, out var error);

        Assert.False(isWatched);
        Assert.StartsWith(missing, error);
        Assert.Equal(ThemeFolderEventKind.Stopped, (await _watch.NextChangeAsync(CancellationToken.None)).Kind);
    }

    [Fact]
    public async Task Removing_the_watched_folder_loses_the_watch_and_says_so()
    {
        var folder = Folder;
        Assert.True(_watch.TryStart(folder, out _));
        var waiting = _watch.NextChangeAsync(CancellationToken.None);

        Directory.Delete(folder, recursive: true);
        var news = await waiting.WaitAsync(Patience);

        Assert.Equal(ThemeFolderEventKind.Lost, news.Kind);
        Assert.StartsWith(folder, news.Reason);
        Assert.Equal(ThemeFolderEventKind.Stopped, (await _watch.NextChangeAsync(CancellationToken.None)).Kind);
    }
}
