using System.IO.Pipes;
using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class ExternalCommandPipeTests
{
    private const string GreetId = "M:ILSight.App.Tests.Fixtures.Greeter.Greet(System.Int32)";

    private static ExternalCommand Command(params string[] args) => CommandLine.Parse(args).Command;

    private static List<ExternalCommand> TakeArrived(ExternalCommandInbox inbox, int count)
    {
        inbox.Close();
        var arrived = new List<ExternalCommand>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var reading = Task.Run(async () =>
        {
            await foreach (var command in inbox.ReadAllAsync().WithCancellation(timeout.Token))
                arrived.Add(command);
        });
        reading.GetAwaiter().GetResult();
        Assert.Equal(count, arrived.Count);
        return arrived;
    }

    [Fact]
    public void The_receiver_takes_the_command_a_later_run_sends()
    {
        using var temp = new TempDir();
        var address = ExternalCommandAddress.ForSettingsFile(temp.PathOf("settings.json"));
        using var receiver = ExternalCommandReceiver.TryClaim(address);
        Assert.NotNull(receiver);

        var handedOver = ExternalCommandPipe.TrySend(address, Command("--navigateto=T:Some.Type", "relative.dll"));

        Assert.True(handedOver);
        var handed = Assert.Single(TakeArrived(receiver.Inbox, 1));
        Assert.Equal("T:Some.Type", handed.NavigateTo);
        Assert.Equal([Path.GetFullPath("relative.dll")], handed.AssemblyPaths.Select(static p => p.Text));
    }

    [Fact]
    public void A_later_run_is_refused_once_the_inbox_closes()
    {
        using var temp = new TempDir();
        var address = ExternalCommandAddress.ForSettingsFile(temp.PathOf("settings.json"));
        using var receiver = ExternalCommandReceiver.TryClaim(address);
        Assert.NotNull(receiver);
        receiver.Inbox.Close();

        var handedOver = ExternalCommandPipe.TrySend(address, Command("a.dll"));

        Assert.False(handedOver);
    }

    [Fact]
    public void A_run_with_no_one_listening_cannot_hand_over()
    {
        using var temp = new TempDir();
        var address = ExternalCommandAddress.ForSettingsFile(temp.PathOf("settings.json"));

        var handedOver = ExternalCommandPipe.TrySend(address, Command("a.dll"));

        Assert.False(handedOver);
    }

    [Fact]
    public void Many_runs_starting_together_all_reach_the_receiver()
    {
        using var temp = new TempDir();
        var address = ExternalCommandAddress.ForSettingsFile(temp.PathOf("settings.json"));
        using var receiver = ExternalCommandReceiver.TryClaim(address);
        Assert.NotNull(receiver);

        var sends = Enumerable.Range(0, 16).Select(i => Task.Run(() => ExternalCommandPipe.TrySend(address, Command(i + ".dll")))).ToArray();
        Task.WaitAll(sends);

        Assert.All(sends, static send => Assert.True(send.Result));
        Assert.Equal(16, TakeArrived(receiver.Inbox, 16).Count);
    }

    [Fact]
    public void A_run_that_goes_away_partway_does_not_stop_the_receiver()
    {
        using var temp = new TempDir();
        var address = ExternalCommandAddress.ForSettingsFile(temp.PathOf("settings.json"));
        using var receiver = ExternalCommandReceiver.TryClaim(address);
        Assert.NotNull(receiver);
        using (var gone = new NamedPipeClientStream(".", address.Name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
        {
            gone.Connect(5000);
        }

        var handedOver = ExternalCommandPipe.TrySend(address, Command("a.dll"));

        Assert.True(handedOver);
    }

    [Fact]
    public void A_command_handed_to_the_open_window_opens_its_files_shows_its_member_and_brings_the_window_to_the_front()
    {
        using var temp = new TempDir();
        var address = ExternalCommandAddress.ForSettingsFile(temp.PathOf("settings.json"));
        using var receiver = ExternalCommandReceiver.TryClaim(address);
        Assert.NotNull(receiver);
        var app = new AppDriver(inbox: receiver.Inbox);
        app.Run();

        Assert.True(ExternalCommandPipe.TrySend(address, Command("--navigateto=" + GreetId, AppStateDriver.FixturePath)));
        app.RunUntil(() => app.Frames.FrontRequested);
        app.RunUntil(() => app.AppState.Tabs.Active?.Node?.Icon == NodeIcon.Method);
        app.Settle(1);

        Assert.Equal("Greet(int) : string", app.AppState.Tabs.Active!.Node!.Text);
        Assert.Equal([AppStateDriver.FixturePath], app.AppState.Assemblies.FilePaths.Select(static p => p.Text));
    }

    [Fact]
    public void A_command_handed_over_while_the_app_starts_is_followed_once_it_runs()
    {
        var app = new AppDriver(queuedCommand: Command("--language=IL"));

        app.RunUntil(() => app.AppState.Language == DecompilationLanguage.IL);

        Assert.Equal(DecompilationLanguage.IL, app.AppState.Language);
    }
}
