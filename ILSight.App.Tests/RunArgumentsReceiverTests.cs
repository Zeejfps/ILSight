using System.Threading.Channels;
using ILSight.Core;
using ILSight.CommandArea;
using ILSight.ILSightAppArea;

namespace ILSight.App.Tests;

public sealed class RunArgumentsReceiverTests
{
    private static RunArguments Arguments(params string[] args)
    {
        return CommandLines.Parse(args).Arguments;
    }

    [Fact]
    public async Task The_first_run_with_some_settings_gets_the_role_and_later_ones_do_not()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));

        using var first = await RunPipes.ClaimAsync(address);
        using var later = await RunPipes.ClaimAsync(address);

        Assert.NotNull(first);
        Assert.Null(later);
    }

    [Fact]
    public async Task Runs_with_other_settings_each_get_the_role()
    {
        using var temp = new TempDir();

        using var one = await RunPipes.ClaimAsync(RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("one.json"))));
        using var other = await RunPipes.ClaimAsync(RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("other.json"))));

        Assert.NotNull(one);
        Assert.NotNull(other);
    }

    [Fact]
    public async Task Once_the_receiver_ends_the_next_run_gets_the_role()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        var first = await RunPipes.ClaimAsync(address);
        Assert.NotNull(first);
        first.Dispose();

        using var next = await RunPipes.ClaimAsync(address);

        Assert.NotNull(next);
    }

    [Fact]
    public async Task A_run_that_did_not_get_the_role_does_not_keep_it_from_the_next_one()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        var first = await RunPipes.ClaimAsync(address);
        Assert.NotNull(first);
        Assert.Null(await RunPipes.ClaimAsync(address));
        first.Dispose();

        using var next = await RunPipes.ClaimAsync(address);

        Assert.NotNull(next);
    }

    [Fact]
    public async Task A_run_standing_by_takes_arguments_once_the_receiver_ends()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        var first = await RunPipes.ClaimAsync(address);
        Assert.NotNull(first);
        using var standing = RunArgumentsReceiver.StandBy(address, Channel.CreateUnbounded<RunArguments>());

        first.Dispose();
        var handedOver = await RunPipes.TrySendAsync(address, Arguments("a.dll"));

        Assert.True(handedOver);
    }

    [Fact]
    public async Task A_run_standing_by_ends_though_the_role_never_came_free()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        using var first = await RunPipes.ClaimAsync(address);

        var standing = RunArgumentsReceiver.StandBy(address, Channel.CreateUnbounded<RunArguments>());
        standing.Dispose();

        Assert.True(await RunPipes.TrySendAsync(address, Arguments("a.dll")));
    }
}
