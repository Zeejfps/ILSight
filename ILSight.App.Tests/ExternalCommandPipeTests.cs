using System.Threading.Channels;
using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class ExternalCommandPipeTests
{
    private const string GreetId = "M:ILSight.App.Tests.Fixtures.Greeter.Greet(System.Int32)";

    private sealed class ChannelHandler : IExternalCommandHandler
    {
        public Channel<ExternalCommand> Commands { get; } = Channel.CreateUnbounded<ExternalCommand>();

        public bool TryHandle(ExternalCommand command)
        {
            return Commands.Writer.TryWrite(command);
        }
    }

    [Fact]
    public void The_run_forwarding_takes_the_command_a_later_run_hands_over()
    {
        using var temp = new TempDir();
        var address = ExternalCommandAddress.ForSettingsFile(temp.PathOf("settings.json"));
        var handler = new ChannelHandler();
        using var first = new ExternalCommandPipe(address);
        first.ForwardTo(handler);
        using var later = new ExternalCommandPipe(address);

        var handedOver = later.TryHandOverToRunningApp(CommandLine.Parse(["--navigateto=T:Some.Type", "relative.dll"]).Command);

        Assert.True(handedOver);
        Assert.True(handler.Commands.Reader.TryRead(out var handed));
        Assert.Equal("T:Some.Type", handed.NavigateTo);
        Assert.Equal([Path.GetFullPath("relative.dll")], handed.AssemblyPaths.Select(static p => p.Text));
    }

    [Fact]
    public void A_later_run_is_refused_once_the_app_takes_no_more_command_lines()
    {
        using var temp = new TempDir();
        var address = ExternalCommandAddress.ForSettingsFile(temp.PathOf("settings.json"));
        var handler = new ChannelHandler();
        using var first = new ExternalCommandPipe(address);
        first.ForwardTo(handler);
        handler.Commands.Writer.TryComplete();
        using var later = new ExternalCommandPipe(address);

        var handedOver = later.TryHandOverToRunningApp(CommandLine.Parse(["a.dll"]).Command);

        Assert.False(handedOver);
    }

    [Fact]
    public void A_run_with_no_one_listening_cannot_hand_over()
    {
        using var temp = new TempDir();
        var address = ExternalCommandAddress.ForSettingsFile(temp.PathOf("settings.json"));
        using var pipe = new ExternalCommandPipe(address);

        var handedOver = pipe.TryHandOverToRunningApp(CommandLine.Parse(["a.dll"]).Command);

        Assert.False(handedOver);
    }

    [Fact]
    public void A_command_handed_to_the_open_window_opens_its_files_shows_its_member_and_brings_the_window_to_the_front()
    {
        using var temp = new TempDir();
        var address = ExternalCommandAddress.ForSettingsFile(temp.PathOf("settings.json"));
        var handler = new ChannelHandler();
        using var first = new ExternalCommandPipe(address);
        first.ForwardTo(handler);
        var app = new AppDriver(pendingCommands: handler.Commands);
        app.Run();
        using var later = new ExternalCommandPipe(address);

        Assert.True(later.TryHandOverToRunningApp(CommandLine.Parse(["--navigateto=" + GreetId, AppStateDriver.FixturePath]).Command));
        app.RunUntil(() => app.Frames.FrontRequested);
        app.RunUntil(() => app.AppState.Tabs.Active?.Node?.Icon == NodeIcon.Method);
        app.Settle(1);

        Assert.Equal("Greet(int) : string", app.AppState.Tabs.Active!.Node!.Text);
        Assert.Equal([AppStateDriver.FixturePath], app.AppState.Assemblies.FilePaths.Select(static p => p.Text));
    }

    [Fact]
    public void A_command_handed_over_while_the_app_starts_is_followed_once_it_runs()
    {
        var handler = new ChannelHandler();
        Assert.True(handler.TryHandle(CommandLine.Parse(["--language=IL"]).Command));
        var app = new AppDriver(pendingCommands: handler.Commands);

        app.RunUntil(() => app.AppState.Language == DecompilationLanguage.IL);

        Assert.Equal(DecompilationLanguage.IL, app.AppState.Language);
    }
}
