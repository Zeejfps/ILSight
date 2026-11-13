using System.Threading.Channels;
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
        var ports = new FakeRunRoutingPorts { Claims = false, Answers = [HandOverAnswer.Taken] };

        var route = await new RunRouting(ports).RouteAsync(Arguments("a.dll"), newInstance: true, CancellationToken.None);

        Assert.Equal(RunRoute.StandsBy, route);
        Assert.Empty(ports.Sent);
    }

    [Fact]
    public async Task RouteAsync_RoleHeldAndTheHolderTakesTheArguments_HandsThemOver()
    {
        var ports = new FakeRunRoutingPorts { Claims = false, Answers = [HandOverAnswer.Taken] };
        var arguments = Arguments("a.dll");

        var route = await new RunRouting(ports).RouteAsync(arguments, newInstance: false, CancellationToken.None);

        Assert.Equal(RunRoute.HandedOver, route);
        Assert.Equal([arguments], ports.Sent);
    }

    [Fact]
    public async Task RouteAsync_RoleHeldAndTheHolderDoesNotTakeThem_StandsByAfterSending()
    {
        var ports = new FakeRunRoutingPorts { Claims = false, Answers = [HandOverAnswer.NotTaken] };
        var arguments = Arguments("a.dll");

        var route = await new RunRouting(ports).RouteAsync(arguments, newInstance: false, CancellationToken.None);

        Assert.Equal(RunRoute.StandsBy, route);
        Assert.Equal([arguments], ports.Sent);
    }

    [Fact]
    public async Task RouteAsync_AnAnswerLostThenTaken_SendsAgainUnderTheSameRequestAndHandsThemOver()
    {
        var ports = new FakeRunRoutingPorts { Claims = false, Answers = [HandOverAnswer.Unknown, HandOverAnswer.Taken] };
        var arguments = Arguments("a.dll");

        var route = await new RunRouting(ports).RouteAsync(arguments, newInstance: false, CancellationToken.None);

        Assert.Equal(RunRoute.HandedOver, route);
        Assert.Equal([arguments, arguments], ports.Sent);
        Assert.Single(ports.Requests.Distinct());
        Assert.Empty(ports.SaidUnconfirmed);
    }

    [Fact]
    public async Task RouteAsync_NoAnswerAfterTheLastSend_SaysSoAndOpensNoWindow()
    {
        var ports = new FakeRunRoutingPorts { Claims = false, Answers = [HandOverAnswer.Unknown] };
        var arguments = Arguments("a.dll");

        var route = await new RunRouting(ports).RouteAsync(arguments, newInstance: false, CancellationToken.None);

        Assert.Equal(RunRoute.Unconfirmed, route);
        Assert.Equal(RunRouting.MaxSends, ports.Sent.Count);
        Assert.Single(ports.Requests.Distinct());
        Assert.Equal([arguments], ports.SaidUnconfirmed);
    }

    [Fact]
    public async Task The_adapter_claims_a_free_role_is_refused_a_held_one_and_sends_to_its_holder_until_it_stops_taking()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        var first = new RunRoutingAdapter(address, Channel.CreateUnbounded<RunArguments>());
        var later = new RunRoutingAdapter(address, Channel.CreateUnbounded<RunArguments>());

        var isFirstClaimed = await first.TryClaimAsync(CancellationToken.None);
        using var holder = first.Claimed;
        var isLaterClaimed = await later.TryClaimAsync(CancellationToken.None);
        var answered = await later.SendAsync(Arguments("a.dll"), Guid.NewGuid(), CancellationToken.None);
        Assert.NotNull(holder);
        holder.Inbox.Writer.TryComplete();
        var refused = await later.SendAsync(Arguments("b.dll"), Guid.NewGuid(), CancellationToken.None);
        holder.Dispose();
        var unanswered = await later.SendAsync(Arguments("c.dll"), Guid.NewGuid(), CancellationToken.None);

        Assert.True(isFirstClaimed);
        Assert.False(isLaterClaimed);
        Assert.Null(later.Claimed);
        Assert.Equal(HandOverAnswer.Taken, answered);
        Assert.Equal(HandOverAnswer.NotTaken, refused);
        Assert.Equal(HandOverAnswer.NotTaken, unanswered);
    }
}
