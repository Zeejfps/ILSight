using ILSight.Core;
using ILSight.CommandArea;
using ILSight.ILSightAppArea;

namespace ILSight.App.Tests;

public sealed class RunRoutingTests
{
    private static RunArguments Arguments(params string[] args)
    {
        return CommandLines.Parse(args).Arguments;
    }

    [Fact]
    public async Task RouteAsync_RoleFree_ReceivesAndSendsNothing()
    {
        var ports = new FakeRunRoutingPorts { Claims = true };

        var route = await new RunRouting(ports).RouteAsync(Arguments("a.dll"), newInstance: false, CancellationToken.None);

        Assert.Equal(RunRoute.Receives, route);
        Assert.Empty(ports.Sent);
    }

    [Fact]
    public async Task RouteAsync_RoleHeldAndANewInstanceAskedFor_StandsByAndSendsNothing()
    {
        var ports = new FakeRunRoutingPorts { Claims = false, Takes = true };

        var route = await new RunRouting(ports).RouteAsync(Arguments("a.dll"), newInstance: true, CancellationToken.None);

        Assert.Equal(RunRoute.StandsBy, route);
        Assert.Empty(ports.Sent);
    }

    [Fact]
    public async Task RouteAsync_RoleHeldAndTheHolderTakesTheArguments_HandsThemOver()
    {
        var ports = new FakeRunRoutingPorts { Claims = false, Takes = true };
        var arguments = Arguments("a.dll");

        var route = await new RunRouting(ports).RouteAsync(arguments, newInstance: false, CancellationToken.None);

        Assert.Equal(RunRoute.HandedOver, route);
        Assert.Equal([arguments], ports.Sent);
    }

    [Fact]
    public async Task RouteAsync_RoleHeldAndTheHolderDoesNotTakeThem_StandsByAfterSending()
    {
        var ports = new FakeRunRoutingPorts { Claims = false, Takes = false };
        var arguments = Arguments("a.dll");

        var route = await new RunRouting(ports).RouteAsync(arguments, newInstance: false, CancellationToken.None);

        Assert.Equal(RunRoute.StandsBy, route);
        Assert.Equal([arguments], ports.Sent);
    }

    [Fact]
    public void The_adapter_claims_a_free_role_is_refused_a_held_one_and_sends_to_its_holder_until_it_stops_taking()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        var first = new RunRoutingAdapter(address);
        var later = new RunRoutingAdapter(address);

        var isFirstClaimed = TestWaits.Wait(() => first.TryClaimAsync(CancellationToken.None));
        using var holder = first.Claimed;
        var isLaterClaimed = TestWaits.Wait(() => later.TryClaimAsync(CancellationToken.None));
        var answered = TestWaits.Wait(() => later.TrySendAsync(Arguments("a.dll"), CancellationToken.None));
        Assert.NotNull(holder);
        holder.Inbox.Writer.TryComplete();
        var refused = TestWaits.Wait(() => later.TrySendAsync(Arguments("b.dll"), CancellationToken.None));
        holder.Dispose();
        var unanswered = TestWaits.Wait(() => later.TrySendAsync(Arguments("c.dll"), CancellationToken.None));

        Assert.True(isFirstClaimed);
        Assert.False(isLaterClaimed);
        Assert.Null(later.Claimed);
        Assert.True(answered);
        Assert.False(refused);
        Assert.False(unanswered);
    }
}
