using System.IO.Pipes;
using System.Threading.Channels;
using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class RunArgumentsPipeTests
{
    private const string GreetId = "M:ILSight.App.Tests.Fixtures.Greeter.Greet(System.Int32)";

    private static RunArguments Arguments(params string[] args) => CommandLines.Parse(args).Arguments;

    private static List<RunArguments> TakeArrived(Channel<RunArguments> inbox, int count)
    {
        inbox.Writer.TryComplete();
        var arrived = new List<RunArguments>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var reading = Task.Run(async () =>
        {
            await foreach (var arguments in inbox.Reader.ReadAllAsync().WithCancellation(timeout.Token))
                arrived.Add(arguments);
        });
        reading.GetAwaiter().GetResult();
        Assert.Equal(count, arrived.Count);
        return arrived;
    }

    [Fact]
    public void The_receiver_takes_the_arguments_a_later_run_sends()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.FindAddress(temp.PathOf("settings.json"));
        using var receiver = RunArgumentsReceiver.TryClaim(address);
        Assert.NotNull(receiver);

        var handedOver = RunArgumentsPipe.TrySend(address, Arguments("--navigateto=T:Some.Type", "relative.dll"));

        Assert.True(handedOver);
        var handed = Assert.Single(TakeArrived(receiver.Inbox, 1));
        Assert.Equal("T:Some.Type", handed.Files.NavigateTo);
        Assert.Equal([Path.GetFullPath("relative.dll")], handed.Files.Paths.Select(static p => p.Text));
    }

    [Fact]
    public void A_later_run_is_refused_once_the_inbox_closes()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.FindAddress(temp.PathOf("settings.json"));
        using var receiver = RunArgumentsReceiver.TryClaim(address);
        Assert.NotNull(receiver);
        receiver.Inbox.Writer.TryComplete();

        var handedOver = RunArgumentsPipe.TrySend(address, Arguments("a.dll"));

        Assert.False(handedOver);
    }

    [Fact]
    public void A_run_with_no_one_listening_cannot_hand_over()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.FindAddress(temp.PathOf("settings.json"));

        var handedOver = RunArgumentsPipe.TrySend(address, Arguments("a.dll"));

        Assert.False(handedOver);
    }

    [Fact]
    public void Many_runs_starting_together_all_reach_the_receiver()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.FindAddress(temp.PathOf("settings.json"));
        using var receiver = RunArgumentsReceiver.TryClaim(address);
        Assert.NotNull(receiver);

        var sends = Enumerable.Range(0, 16).Select(i => Task.Run(() => RunArgumentsPipe.TrySend(address, Arguments(i + ".dll")))).ToArray();
        Task.WaitAll(sends);

        Assert.All(sends, static send => Assert.True(send.Result));
        Assert.Equal(16, TakeArrived(receiver.Inbox, 16).Count);
    }

    [Fact]
    public void A_run_that_goes_away_partway_does_not_stop_the_receiver()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.FindAddress(temp.PathOf("settings.json"));
        using var receiver = RunArgumentsReceiver.TryClaim(address);
        Assert.NotNull(receiver);
        using (var gone = new NamedPipeClientStream(".", address.Name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
        {
            gone.Connect(5000);
        }

        var handedOver = RunArgumentsPipe.TrySend(address, Arguments("a.dll"));

        Assert.True(handedOver);
    }

    [Fact]
    public void Arguments_handed_to_the_open_window_open_their_files_show_their_member_and_bring_the_window_to_the_front()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.FindAddress(temp.PathOf("settings.json"));
        using var receiver = RunArgumentsReceiver.TryClaim(address);
        Assert.NotNull(receiver);
        var app = new AppDriver(inbox: receiver.Inbox);
        app.Run();

        Assert.True(RunArgumentsPipe.TrySend(address, Arguments("--navigateto=" + GreetId, AppStateDriver.FixturePath)));
        app.RunUntil(() => app.Frames.FrontRequested);
        app.RunUntil(() => app.AppState.Tabs.Active?.Node?.Icon == NodeIcon.Method);
        app.Settle(1);

        Assert.Equal("Greet(int) : string", app.AppState.Tabs.Active!.Node!.Text);
        Assert.Equal([AppStateDriver.FixturePath], app.AppState.Assemblies.FilePaths.Select(static p => p.Text));
    }

    [Fact]
    public void Arguments_handed_over_while_the_app_starts_are_followed_once_it_runs()
    {
        var app = new AppDriver(queuedArguments: Arguments("--language=IL"));

        app.RunUntil(() => app.AppState.Language == DecompilationLanguage.IL);

        Assert.Equal(DecompilationLanguage.IL, app.AppState.Language);
    }
}
