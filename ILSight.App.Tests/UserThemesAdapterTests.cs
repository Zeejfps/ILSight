using ILSight.UserState;

namespace ILSight.App.Tests;

/// <summary>The adapter over a real folder: what it does with the watch as the app shuts down.</summary>
public sealed class UserThemesAdapterTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

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
        var waiting = adapter.WaitForChangeAsync(shutdown.Token);

        await shutdown.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(Patience));
        var after = await adapter.WaitForChangeAsync(CancellationToken.None).WaitAsync(Patience);
        Assert.Equal(ThemeFolderEventKind.Stopped, Assert.IsType<Outcome<ThemeFolderEvent>.Ok>(after).Value.Kind);
    }

    [Fact]
    public void ReadThemeFile_FileMissing_GivesAThemeThatSaysItCannotBeRead()
    {
        var adapter = new UserColorThemesAdapter(new ErrorLog());
        var path = _temp.PathOf("themes/gone.json");

        var theme = adapter.ReadThemeFile(ThemeId<ThemeFile>.Of("gone"), path);

        Assert.Null(theme.Contents);
        Assert.Equal(ThemeProblem.Unreadable, Assert.Single(theme.Errors).Problem);
    }
}
