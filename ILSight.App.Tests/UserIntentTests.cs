namespace ILSight.App.Tests;

public sealed class UserIntentTests
{
    [Fact]
    public async Task ResolveAsync_LookupFindsThePlace_IsFoundAndStopsResolving()
    {
        var intent = new UserIntent();

        var resolved = await intent.ResolveAsync(static () => "place", CancellationToken.None);

        Assert.Equal("place", Assert.IsType<Resolution<string>.Found>(resolved).Value);
        Assert.False(intent.IsResolving);
    }

    [Fact]
    public async Task ResolveAsync_LookupFindsNothing_IsMissing()
    {
        var intent = new UserIntent();

        var resolved = await intent.ResolveAsync(static () => (string?)null, CancellationToken.None);

        Assert.IsType<Resolution<string>.Missing>(resolved);
        Assert.False(intent.IsResolving);
    }

    [Fact]
    public async Task ResolveAsync_LookupThrows_IsFailedWithTheError()
    {
        var intent = new UserIntent();
        var error = new InvalidOperationException("unreadable");

        var resolved = await intent.ResolveAsync<string>(() => throw error, CancellationToken.None);

        Assert.Same(error, Assert.IsType<Resolution<string>.Failed>(resolved).Error);
        Assert.False(intent.IsResolving);
    }

    [Fact]
    public async Task ResolveAsync_ValueTypeLookup_FindsTheValueOrNothing()
    {
        var intent = new UserIntent();

        var found = await intent.ResolveAsync(static () => (int?)7, CancellationToken.None);
        var missing = await intent.ResolveAsync(static () => (int?)null, CancellationToken.None);

        Assert.Equal(7, Assert.IsType<Resolution<int>.Found>(found).Value);
        Assert.IsType<Resolution<int>.Missing>(missing);
    }

    [Fact]
    public async Task ResolveAsync_AnotherRequestBeginsMeanwhile_IsStaleAndLeavesThatRequestAlone()
    {
        var intent = new UserIntent();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var resolving = intent.ResolveAsync(() =>
        {
            started.Set();
            release.Wait();
            return "place";
        }, CancellationToken.None);
        started.Wait();
        Assert.True(intent.IsResolving);
        var next = intent.Begin();
        intent.StartResolving(next);
        release.Set();
        var resolved = await resolving;

        Assert.IsType<Resolution<string>.Stale>(resolved);
        Assert.True(intent.IsResolving);
        Assert.True(intent.IsCurrent(next));
    }
}
