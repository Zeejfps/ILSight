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

        using var first = RunArgumentsPipe.TryClaim(address);
        using var later = RunArgumentsPipe.TryClaim(address);

        Assert.NotNull(first);
        Assert.Null(later);
    }

    [Fact]
    public void Runs_with_other_settings_each_get_the_role()
    {
        using var temp = new TempDir();

        using var one = RunArgumentsPipe.TryClaim(RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("one.json"))));
        using var other = RunArgumentsPipe.TryClaim(RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("other.json"))));

        Assert.NotNull(one);
        Assert.NotNull(other);
    }

    [Fact]
    public void Once_the_receiver_ends_the_next_run_gets_the_role()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        var first = RunArgumentsPipe.TryClaim(address);
        Assert.NotNull(first);
        first.Dispose();

        using var next = RunArgumentsPipe.TryClaim(address);

        Assert.NotNull(next);
    }

    [Fact]
    public void A_run_that_did_not_get_the_role_does_not_keep_it_from_the_next_one()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        var first = RunArgumentsPipe.TryClaim(address);
        Assert.NotNull(first);
        Assert.Null(RunArgumentsPipe.TryClaim(address));
        first.Dispose();

        using var next = RunArgumentsPipe.TryClaim(address);

        Assert.NotNull(next);
    }

    [Fact]
    public void A_run_standing_by_takes_arguments_once_the_receiver_ends()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        var first = RunArgumentsPipe.TryClaim(address);
        Assert.NotNull(first);
        using var standing = RunArgumentsPipe.StandBy(address);

        first.Dispose();
        var handedOver = RunArgumentsPipe.TrySend(address, Arguments("a.dll"));

        Assert.True(handedOver);
    }

    [Fact]
    public void A_run_standing_by_ends_though_the_role_never_came_free()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        using var first = RunArgumentsPipe.TryClaim(address);

        var standing = RunArgumentsPipe.StandBy(address);
        standing.Dispose();

        Assert.True(RunArgumentsPipe.TrySend(address, Arguments("a.dll")));
    }

    [Fact]
    public void A_run_is_forwarded_when_a_receiver_takes_its_arguments()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        using var first = RunArgumentsRouting.ForwardOrReceive(address, Arguments("a.dll"), newInstance: false);

        using var later = RunArgumentsRouting.ForwardOrReceive(address, Arguments("b.dll"), newInstance: false);

        Assert.NotNull(first);
        Assert.Null(later);
    }

    [Fact]
    public void A_run_asking_for_a_new_instance_stands_by_without_sending()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        using var first = RunArgumentsRouting.ForwardOrReceive(address, Arguments("a.dll"), newInstance: false);
        Assert.NotNull(first);

        using var later = RunArgumentsRouting.ForwardOrReceive(address, Arguments("b.dll"), newInstance: true);

        Assert.NotNull(later);
        first.Inbox.Writer.TryComplete();
        Assert.Empty(first.Inbox.Reader.ReadAllAsync().ToBlockingEnumerable());
    }

    [Fact]
    public void A_run_refused_by_a_closing_window_opens_its_own_and_takes_over()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        var first = RunArgumentsRouting.ForwardOrReceive(address, Arguments("a.dll"), newInstance: false);
        Assert.NotNull(first);
        first.Inbox.Writer.TryComplete();

        using var later = RunArgumentsRouting.ForwardOrReceive(address, Arguments("b.dll"), newInstance: false);
        Assert.NotNull(later);
        first.Dispose();

        Assert.True(RunArgumentsPipe.TrySend(address, Arguments("c.dll")));
    }
}
