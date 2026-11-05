using System.Threading.Channels;
using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class CommandLinePipeTests
{
    private const string GreetId = "M:ILSight.App.Tests.Fixtures.Greeter.Greet(System.Int32)";

    [Fact]
    public void The_run_listening_takes_the_command_line_a_later_run_hands_over()
    {
        using var temp = new TempDir();
        using var instanceLock = SingleInstanceLock.Request(temp.PathOf("settings.json"));
        var laterRuns = Channel.CreateUnbounded<CommandLine>();
        using var pipe = CommandLinePipe.Listen(instanceLock.Name, laterRuns.Writer);

        var handedOver = CommandLinePipe.TryHandOver(instanceLock.Name, CommandLine.Parse(["--navigateto=T:Some.Type", "relative.dll"]));

        Assert.True(handedOver);
        Assert.True(laterRuns.Reader.TryRead(out var handed));
        Assert.Equal("T:Some.Type", handed.NavigateTo);
        Assert.Equal([Path.GetFullPath("relative.dll")], handed.AssemblyPaths.Select(static p => p.Text));
    }

    [Fact]
    public void A_later_run_is_refused_once_the_window_takes_no_more_command_lines()
    {
        using var temp = new TempDir();
        using var instanceLock = SingleInstanceLock.Request(temp.PathOf("settings.json"));
        var laterRuns = Channel.CreateUnbounded<CommandLine>();
        using var pipe = CommandLinePipe.Listen(instanceLock.Name, laterRuns.Writer);
        laterRuns.Writer.TryComplete();

        var handedOver = CommandLinePipe.TryHandOver(instanceLock.Name, CommandLine.Parse(["a.dll"]));

        Assert.False(handedOver);
    }

    [Fact]
    public void A_command_line_handed_to_the_open_window_opens_its_files_shows_its_member_and_brings_the_window_to_the_front()
    {
        using var temp = new TempDir();
        using var instanceLock = SingleInstanceLock.Request(temp.PathOf("settings.json"));
        var laterRuns = Channel.CreateUnbounded<CommandLine>();
        using var pipe = CommandLinePipe.Listen(instanceLock.Name, laterRuns.Writer);
        var app = new AppDriver(laterRuns: laterRuns);
        app.Run();

        Assert.True(CommandLinePipe.TryHandOver(instanceLock.Name, CommandLine.Parse(["--navigateto=" + GreetId, AppStateDriver.FixturePath])));
        app.RunUntil(() => app.Frames.FrontRequested);
        app.RunUntil(() => app.AppState.Tabs.Active?.Node?.Icon == NodeIcon.Method);
        app.Settle(1);

        Assert.Equal("Greet(int) : string", app.AppState.Tabs.Active!.Node!.Text);
        Assert.Equal([AppStateDriver.FixturePath], app.AppState.Assemblies.FilePaths.Select(static p => p.Text));
    }

    [Fact]
    public void A_command_line_handed_over_while_the_app_starts_is_followed_once_it_runs()
    {
        var laterRuns = Channel.CreateUnbounded<CommandLine>();
        laterRuns.Writer.TryWrite(CommandLine.Parse(["--language=IL"]));
        var app = new AppDriver(laterRuns: laterRuns);

        app.RunUntil(() => app.AppState.Language == DecompilationLanguage.IL);

        Assert.Equal(DecompilationLanguage.IL, app.AppState.Language);
    }
}
