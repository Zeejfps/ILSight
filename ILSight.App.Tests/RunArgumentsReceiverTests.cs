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
    public void The_first_run_with_some_settings_gets_the_role_and_later_ones_do_not()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));

        using var first = RunPipes.Claim(address);
        using var later = RunPipes.Claim(address);

        Assert.NotNull(first);
        Assert.Null(later);
    }

    [Fact]
    public void Runs_with_other_settings_each_get_the_role()
    {
        using var temp = new TempDir();

        using var one = RunPipes.Claim(RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("one.json"))));
        using var other = RunPipes.Claim(RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("other.json"))));

        Assert.NotNull(one);
        Assert.NotNull(other);
    }

    [Fact]
    public void Once_the_receiver_ends_the_next_run_gets_the_role()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        var first = RunPipes.Claim(address);
        Assert.NotNull(first);
        first.Dispose();

        using var next = RunPipes.Claim(address);

        Assert.NotNull(next);
    }

    [Fact]
    public void A_run_that_did_not_get_the_role_does_not_keep_it_from_the_next_one()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        var first = RunPipes.Claim(address);
        Assert.NotNull(first);
        Assert.Null(RunPipes.Claim(address));
        first.Dispose();

        using var next = RunPipes.Claim(address);

        Assert.NotNull(next);
    }

    [Fact]
    public void A_run_standing_by_takes_arguments_once_the_receiver_ends()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        var first = RunPipes.Claim(address);
        Assert.NotNull(first);
        using var standing = RunArgumentsReceiver.StandBy(address);

        first.Dispose();
        var handedOver = RunPipes.Send(address, Arguments("a.dll"));

        Assert.True(handedOver);
    }

    [Fact]
    public void A_run_standing_by_ends_though_the_role_never_came_free()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        using var first = RunPipes.Claim(address);

        var standing = RunArgumentsReceiver.StandBy(address);
        standing.Dispose();

        Assert.True(RunPipes.Send(address, Arguments("a.dll")));
    }
}
