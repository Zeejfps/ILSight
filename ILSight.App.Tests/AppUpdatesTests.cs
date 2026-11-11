using Velopack;

namespace ILSight.App.Tests;

/// <summary>The update workflows over a fake port: what each does with what the port answers, and the tokens it
/// asks with.</summary>
public sealed class AppUpdatesTests
{
    /// <summary>Answers each port member with what the test set; keeps what was asked and the tokens it was asked
    /// with.</summary>
    private sealed class FakePorts : AppUpdates.IPorts
    {
        public bool IsInstalledCopy { get; init; } = true;

        public Func<CancellationToken, Task<Outcome<UpdateInfo?>>> Find { get; init; } = static _ => AnswerAsync(new Outcome<UpdateInfo?>.Ok(null));

        public bool Downloads { get; init; } = true;

        public bool Applies { get; init; } = true;

        public List<string> Asked { get; } = [];

        public List<CancellationToken> Tokens { get; } = [];

        public bool IsInstalled()
        {
            return IsInstalledCopy;
        }

        public Task<Outcome<UpdateInfo?>> LookUpUpdateAsync(CancellationToken ct)
        {
            Asked.Add(nameof(LookUpUpdateAsync));
            Tokens.Add(ct);
            return Find(ct);
        }

        public Task<bool> TryDownloadAsync(UpdateInfo update, CancellationToken ct)
        {
            Asked.Add(nameof(TryDownloadAsync));
            Tokens.Add(ct);
            return Task.FromResult(Downloads);
        }

        public Task<bool> TryApplyAfterExitAsync(UpdateInfo update, CancellationToken ct)
        {
            Asked.Add(nameof(TryApplyAfterExitAsync));
            Tokens.Add(ct);
            return Task.FromResult(Applies);
        }
    }

    private static UpdateInfo Update(string version)
    {
        return new UpdateInfo(new VelopackAsset { Version = SemanticVersion.Parse(version) }, false);
    }

    private static Task<Outcome<UpdateInfo?>> AnswerAsync(Outcome<UpdateInfo?> answer)
    {
        return Task.FromResult(answer);
    }

    private static Task<Outcome<UpdateInfo?>> FoundAsync(string version)
    {
        return AnswerAsync(new Outcome<UpdateInfo?>.Ok(Update(version)));
    }

    private static async Task<AppUpdates> CreateReadyAsync(FakePorts ports)
    {
        var updates = new AppUpdates(ports);
        await updates.CheckAsync(asked: true, CancellationToken.None);
        Assert.IsType<UpdateStatus.Ready>(updates.Status);
        return updates;
    }

    [Fact]
    public async Task CheckAsync_NewerVersionFound_DownloadsItAndOffersIt()
    {
        var ports = new FakePorts { Find = static _ => FoundAsync("9.0.0") };
        var updates = new AppUpdates(ports);
        using var shutdown = new CancellationTokenSource();

        await updates.CheckAsync(asked: false, shutdown.Token);

        var ready = Assert.IsType<UpdateStatus.Ready>(updates.Status);
        Assert.Equal("9.0.0", AppUpdates.GetVersion(ready.Update));
        Assert.Equal([nameof(FakePorts.LookUpUpdateAsync), nameof(FakePorts.TryDownloadAsync)], ports.Asked);
        Assert.All(ports.Tokens, token => Assert.Equal(shutdown.Token, token));
    }

    [Theory]
    [InlineData(true, typeof(UpdateStatus.UpToDate))]
    [InlineData(false, typeof(UpdateStatus.Idle))]
    public async Task CheckAsync_NothingNewer_SaysSoOnlyWhenAsked(bool asked, Type expected)
    {
        var ports = new FakePorts();
        var updates = new AppUpdates(ports);

        await updates.CheckAsync(asked, CancellationToken.None);

        Assert.IsType(expected, updates.Status);
        Assert.Equal([nameof(FakePorts.LookUpUpdateAsync)], ports.Asked);
    }

    [Theory]
    [InlineData(true, typeof(UpdateStatus.Failed))]
    [InlineData(false, typeof(UpdateStatus.Idle))]
    public async Task CheckAsync_LookingFailed_SaysSoOnlyWhenAskedAndCanCheckAgain(bool asked, Type expected)
    {
        var ports = new FakePorts { Find = static _ => AnswerAsync(new Outcome<UpdateInfo?>.Failed(new HttpRequestException("Offline"))) };
        var updates = new AppUpdates(ports);

        await updates.CheckAsync(asked, CancellationToken.None);

        Assert.IsType(expected, updates.Status);
        Assert.True(updates.CanCheck());
    }

    [Theory]
    [InlineData(true, typeof(UpdateStatus.Failed))]
    [InlineData(false, typeof(UpdateStatus.Idle))]
    public async Task CheckAsync_DownloadFailed_SaysSoOnlyWhenAsked(bool asked, Type expected)
    {
        var ports = new FakePorts { Find = static _ => FoundAsync("9.0.0"), Downloads = false };
        var updates = new AppUpdates(ports);

        await updates.CheckAsync(asked, CancellationToken.None);

        Assert.IsType(expected, updates.Status);
    }

    [Fact]
    public async Task CheckAsync_NotInstalled_AsksNothing()
    {
        var ports = new FakePorts { IsInstalledCopy = false, Find = static _ => FoundAsync("9.0.0") };
        var updates = new AppUpdates(ports);

        await updates.CheckAsync(asked: true, CancellationToken.None);

        Assert.IsType<UpdateStatus.Idle>(updates.Status);
        Assert.Empty(ports.Asked);
        Assert.False(updates.CanCheck());
    }

    [Fact]
    public async Task CheckAsync_WhileOneRuns_StartsNoOther()
    {
        var answer = new TaskCompletionSource<Outcome<UpdateInfo?>>();
        var ports = new FakePorts { Find = _ => answer.Task };
        var updates = new AppUpdates(ports);
        var first = updates.CheckAsync(asked: false, CancellationToken.None);

        await updates.CheckAsync(asked: true, CancellationToken.None);
        answer.SetResult(new Outcome<UpdateInfo?>.Ok(null));
        await first;

        Assert.Single(ports.Asked);
        Assert.IsType<UpdateStatus.Idle>(updates.Status);
    }

    [Fact]
    public async Task CheckAsync_TokenFired_EndsCancelledWithoutAFailure()
    {
        var ports = new FakePorts { Find = static ct => Task.FromCanceled<Outcome<UpdateInfo?>>(ct) };
        var updates = new AppUpdates(ports);
        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => updates.CheckAsync(asked: true, shutdown.Token));

        Assert.IsNotType<UpdateStatus.Failed>(updates.Status);
        Assert.DoesNotContain(nameof(FakePorts.TryDownloadAsync), ports.Asked);
    }

    [Fact]
    public async Task RestartToUpdateAsync_UpdaterStarted_AsksTheAppToClose()
    {
        var ports = new FakePorts { Find = static _ => FoundAsync("9.0.0") };
        var updates = await CreateReadyAsync(ports);
        var viewCommands = new Queue<ViewCommand>();

        await updates.RestartToUpdateAsync(viewCommands, CancellationToken.None);

        Assert.IsType<ViewCommand.Quit>(Assert.Single(viewCommands));
        Assert.IsType<UpdateStatus.Restarting>(updates.Status);
        Assert.Equal(nameof(FakePorts.TryApplyAfterExitAsync), ports.Asked[^1]);
    }

    [Fact]
    public async Task RestartToUpdateAsync_UpdaterNotStarted_LeavesTheAppOpenAndSaysSo()
    {
        var ports = new FakePorts { Find = static _ => FoundAsync("9.0.0"), Applies = false };
        var updates = await CreateReadyAsync(ports);
        var viewCommands = new Queue<ViewCommand>();

        await updates.RestartToUpdateAsync(viewCommands, CancellationToken.None);

        Assert.Empty(viewCommands);
        Assert.IsType<UpdateStatus.Failed>(updates.Status);
    }

    [Fact]
    public async Task RestartToUpdateAsync_NothingReady_DoesNothing()
    {
        var ports = new FakePorts();
        var updates = new AppUpdates(ports);
        var viewCommands = new Queue<ViewCommand>();

        await updates.RestartToUpdateAsync(viewCommands, CancellationToken.None);

        Assert.Empty(viewCommands);
        Assert.Empty(ports.Asked);
        Assert.IsType<UpdateStatus.Idle>(updates.Status);
    }

    [Fact]
    public async Task LookUpUpdateAsync_TokenFired_EndsCancelledRatherThanFailed()
    {
        var adapter = new AppUpdatesAdapter();
        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.LookUpUpdateAsync(shutdown.Token));
    }
}
