using System.Threading.Channels;
using ILSight.Core;
using ILSight.CommandArea;
using ILSight.ILSightAppArea;
using ILSight.SettingArea;

namespace ILSight.App.Tests;

public sealed class RunRouterTests
{
    private static RunArguments Arguments(params string[] args)
    {
        return CommandLines.Parse(args).Arguments;
    }

    private static RunRouterAdapter CreateAdapter(RunArgumentsAddress address, RunNotice notice, Locale locale = Locale.En)
    {
        return new RunRouterAdapter(address, Channel.CreateUnbounded<RunArguments>(), () => Strings.For(locale), notice);
    }

    [Fact]
    public void SayUnconfirmed_WithAConsole_WritesItInTheAppsLanguageToStandardError()
    {
        var error = new StringWriter();
        var adapter = CreateAdapter(new RunArgumentsAddress("unused"), new RunNotice.OnConsole(error), Locale.De);

        var arguments = Arguments("a.dll");

        adapter.SayUnconfirmed(arguments);

        var listed = string.Join(" ", CommandLines.ListArguments(arguments));
        Assert.Equal(Strings.For(Locale.De).ErrorArgumentsUnconfirmed(listed) + Environment.NewLine, error.ToString());
        Assert.Contains("Fenster", error.ToString());
    }

    [Fact]
    public void SayUnconfirmed_WithNoConsole_ShowsItInTheAppsLanguageInADialog()
    {
        var shown = new List<(string Message, string Ok)>();
        var adapter = CreateAdapter(new RunArgumentsAddress("unused"), new RunNotice.InDialog((message, ok) => shown.Add((message, ok))), Locale.Ru);

        var arguments = Arguments("a.dll");

        adapter.SayUnconfirmed(arguments);

        var t = Strings.For(Locale.Ru);
        var listed = string.Join(" ", CommandLines.ListArguments(arguments));
        Assert.Equal([(t.ErrorArgumentsUnconfirmed(listed), t.ErrorArgumentsUnconfirmedOk)], shown);
    }

    [Fact]
    public void An_unconfirmed_hand_over_ends_the_run_with_its_own_exit_status_and_every_other_route_with_success()
    {
        Assert.Equal(RunExitCodes.HandOverUnconfirmed, RunExitCodes.Of(RunRoute.Unconfirmed));
        Assert.NotEqual(RunExitCodes.Success, RunExitCodes.HandOverUnconfirmed);
        Assert.NotEqual(1, RunExitCodes.HandOverUnconfirmed);
        Assert.All([RunRoute.Receives, RunRoute.HandedOver, RunRoute.StandsBy], static route => Assert.Equal(RunExitCodes.Success, RunExitCodes.Of(route)));
    }

    [Fact]
    public void ReadRunStrings_TheLanguageTheSettingsChoose_OrTheSystemsWhenTheyCantBeRead()
    {
        using var temp = new TempDir();
        var settingsPath = FullFilePath.Of(temp.PathOf("settings.json"));
        var settings = temp.Settings();
        settings.Locale = Locale.De;
        Assert.IsType<SettingsWriteResult.Written>(TempSettings.Save(settings));
        var unreadable = FullFilePath.Of(temp.PathOf("unreadable.json"));
        File.WriteAllText(unreadable.Text, "not json");

        Assert.Same(Strings.For(Locale.De), AppRoot.ReadRunStrings(settingsPath));
        Assert.Same(Strings.For(Locales.For(System.Globalization.CultureInfo.CurrentUICulture)), AppRoot.ReadRunStrings(unreadable));
    }

    [Fact]
    public async Task RouteAsync_RoleFree_ReceivesAndSendsNothing()
    {
        var ports = new FakeRunRouterPorts { Claims = true };

        var route = await new RunRouter(ports).RouteAsync(Arguments("a.dll"), newInstance: false, CancellationToken.None);

        Assert.Equal(RunRoute.Receives, route);
        Assert.Empty(ports.Sent);
    }

    [Fact]
    public async Task RouteAsync_RoleHeldAndANewInstanceAskedFor_StandsByAndSendsNothing()
    {
        var ports = new FakeRunRouterPorts { Claims = false, Answers = [HandOverAnswer.Taken] };

        var route = await new RunRouter(ports).RouteAsync(Arguments("a.dll"), newInstance: true, CancellationToken.None);

        Assert.Equal(RunRoute.StandsBy, route);
        Assert.Empty(ports.Sent);
    }

    [Fact]
    public async Task RouteAsync_RoleHeldAndTheHolderTakesTheArguments_HandsThemOver()
    {
        var ports = new FakeRunRouterPorts { Claims = false, Answers = [HandOverAnswer.Taken] };
        var arguments = Arguments("a.dll");

        var route = await new RunRouter(ports).RouteAsync(arguments, newInstance: false, CancellationToken.None);

        Assert.Equal(RunRoute.HandedOver, route);
        Assert.Equal([arguments], ports.Sent);
    }

    [Fact]
    public async Task RouteAsync_RoleHeldAndTheHolderDoesNotTakeThem_StandsByAfterSending()
    {
        var ports = new FakeRunRouterPorts { Claims = false, Answers = [HandOverAnswer.NotTaken] };
        var arguments = Arguments("a.dll");

        var route = await new RunRouter(ports).RouteAsync(arguments, newInstance: false, CancellationToken.None);

        Assert.Equal(RunRoute.StandsBy, route);
        Assert.Equal([arguments], ports.Sent);
    }

    [Fact]
    public async Task RouteAsync_AnAnswerLostThenTaken_SendsAgainUnderTheSameRequestAndHandsThemOver()
    {
        var ports = new FakeRunRouterPorts { Claims = false, Answers = [HandOverAnswer.Unknown, HandOverAnswer.Taken] };
        var arguments = Arguments("a.dll");

        var route = await new RunRouter(ports).RouteAsync(arguments, newInstance: false, CancellationToken.None);

        Assert.Equal(RunRoute.HandedOver, route);
        Assert.Equal([arguments, arguments], ports.Sent);
        Assert.Single(ports.Requests.Distinct());
        Assert.Empty(ports.SaidUnconfirmed);
    }

    [Fact]
    public async Task RouteAsync_NoAnswerAfterTheLastSend_SaysSoAndOpensNoWindow()
    {
        var ports = new FakeRunRouterPorts { Claims = false, Answers = [HandOverAnswer.Unknown] };
        var arguments = Arguments("a.dll");

        var route = await new RunRouter(ports).RouteAsync(arguments, newInstance: false, CancellationToken.None);

        Assert.Equal(RunRoute.Unconfirmed, route);
        Assert.Equal(RunRouter.MaxSends, ports.Sent.Count);
        Assert.Single(ports.Requests.Distinct());
        Assert.Equal([arguments], ports.SaidUnconfirmed);
    }

    [Fact]
    public async Task The_adapter_claims_a_free_role_is_refused_a_held_one_and_sends_to_its_holder_until_it_stops_taking()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        var first = CreateAdapter(address, new RunNotice.OnConsole(TextWriter.Null));
        var later = CreateAdapter(address, new RunNotice.OnConsole(TextWriter.Null));

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
