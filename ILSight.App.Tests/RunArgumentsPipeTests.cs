using System.IO.Pipes;
using System.Threading.Channels;
using ILSight.Core;
using ILSight.CommandArea;
using ILSight.ILSightAppArea;

namespace ILSight.App.Tests;

public sealed class RunArgumentsPipeTests
{
    private const string GreetId = "M:ILSight.App.Tests.Fixtures.Greeter.Greet(System.Int32)";

    private static RunArguments Arguments(params string[] args)
    {
        return CommandLines.Parse(args).Arguments;
    }

    private static async Task<List<RunArguments>> TakeArrivedAsync(Channel<RunArguments> inbox, int count)
    {
        inbox.Writer.TryComplete();
        var arrived = new List<RunArguments>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var arguments in inbox.Reader.ReadAllAsync().WithCancellation(timeout.Token))
        {
            arrived.Add(arguments);
        }

        Assert.Equal(count, arrived.Count);
        return arrived;
    }

    [Fact]
    public async Task The_receiver_takes_the_arguments_a_later_run_sends()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        using var receiver = await RunPipes.ClaimAsync(address);
        Assert.NotNull(receiver);

        var handedOver = await RunPipes.TrySendAsync(address, Arguments("--navigateto=T:Some.Type", "relative.dll"));

        Assert.True(handedOver);
        var handed = Assert.Single(await TakeArrivedAsync(receiver.Inbox, 1));
        Assert.Equal("T:Some.Type", handed.Files.NavigateTo);
        Assert.Equal([Path.GetFullPath("relative.dll")], handed.Files.Paths.Select(static p => p.Text));
    }

    [Fact]
    public async Task A_request_sent_again_is_acknowledged_again_and_taken_once()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        using var receiver = await RunPipes.ClaimAsync(address);
        Assert.NotNull(receiver);
        var request = Guid.NewGuid();

        var first = await RunPipes.SendAsync(address, Arguments("a.dll"), request);
        var again = await RunPipes.SendAsync(address, Arguments("a.dll"), request);
        var other = await RunPipes.SendAsync(address, Arguments("a.dll"), Guid.NewGuid());

        Assert.Equal(HandOverAnswer.Taken, first);
        Assert.Equal(HandOverAnswer.Taken, again);
        Assert.Equal(HandOverAnswer.Taken, other);
        await TakeArrivedAsync(receiver.Inbox, 2);
    }

    [Fact]
    public async Task Arguments_sent_whose_acknowledgement_never_comes_are_unknown()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        await using var server = new NamedPipeServerStream(address.Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var serving = TakeWithoutAnsweringAsync(server);

        var answer = await RunPipes.SendAsync(address, Arguments("a.dll"), Guid.NewGuid());
        await serving;

        Assert.Equal(HandOverAnswer.Unknown, answer);
    }

    // Reads what a run sends as the receiver does, then hangs up without answering: an acknowledgement lost after the
    // receiver took them.
    private static async Task TakeWithoutAnsweringAsync(NamedPipeServerStream server)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await server.WaitForConnectionAsync(timeout.Token);
        await server.WriteAsync(BitConverter.GetBytes(Environment.ProcessId), timeout.Token);
        await server.ReadExactlyAsync(new byte[16], timeout.Token);
        var count = await ReadIntAsync(server, timeout.Token);
        for (var i = 0; i < count; i++)
        {
            await server.ReadExactlyAsync(new byte[await ReadIntAsync(server, timeout.Token)], timeout.Token);
        }

        server.Disconnect();
    }

    private static async Task<int> ReadIntAsync(Stream pipe, CancellationToken ct)
    {
        var bytes = new byte[4];
        await pipe.ReadExactlyAsync(bytes, ct);
        return BitConverter.ToInt32(bytes);
    }

    [Fact]
    public async Task A_later_run_is_refused_once_the_inbox_closes()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        using var receiver = await RunPipes.ClaimAsync(address);
        Assert.NotNull(receiver);
        receiver.Inbox.Writer.TryComplete();

        var handedOver = await RunPipes.TrySendAsync(address, Arguments("a.dll"));

        Assert.False(handedOver);
    }

    [Fact]
    public async Task A_run_with_no_one_listening_cannot_hand_over()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));

        var handedOver = await RunPipes.TrySendAsync(address, Arguments("a.dll"));

        Assert.False(handedOver);
    }

    [Fact]
    public async Task Many_runs_starting_together_all_reach_the_receiver()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        using var receiver = await RunPipes.ClaimAsync(address);
        Assert.NotNull(receiver);

        var sends = Enumerable.Range(0, 16).Select(i => Task.Run(() => RunPipes.TrySendAsync(address, Arguments(i + ".dll")))).ToArray();
        var sent = await Task.WhenAll(sends);

        Assert.All(sent, Assert.True);
        Assert.Equal(16, (await TakeArrivedAsync(receiver.Inbox, 16)).Count);
    }

    [Fact]
    public async Task A_run_that_goes_away_partway_does_not_stop_the_receiver()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        using var receiver = await RunPipes.ClaimAsync(address);
        Assert.NotNull(receiver);
        using (var gone = new NamedPipeClientStream(".", address.Name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
        {
            await gone.ConnectAsync(5000);
        }

        var handedOver = await RunPipes.TrySendAsync(address, Arguments("a.dll"));

        Assert.True(handedOver);
    }

    [Fact]
    public async Task Arguments_handed_to_the_open_window_open_their_files_show_their_member_and_bring_the_window_to_the_front()
    {
        using var temp = new TempDir();
        var address = RunArgumentsRouting.GetAddress(FullFilePath.Of(temp.PathOf("settings.json")));
        using var receiver = await RunPipes.ClaimAsync(address);
        Assert.NotNull(receiver);
        var app = new AppDriver(inbox: receiver.Inbox);
        app.Run();

        Assert.True(await RunPipes.TrySendAsync(address, Arguments("--navigateto=" + GreetId, AppStateDriver.FixturePath)));
        app.Frames.WaitUntil(() => app.Frames.FrontRequested, Patience);
        app.Frames.WaitUntil(() => app.AppState.Navigation.ActiveTab?.Node?.Icon == NodeIcon.Method, Patience);
        app.Settle(1);

        Assert.Equal("Greet(int) : string", app.AppState.Navigation.ActiveTab?.Node?.Text);
        Assert.Equal([AppStateDriver.FixturePath], app.AppState.Assemblies.ListFilePaths().Select(static p => p.Text));
    }

    [Fact]
    public void Arguments_handed_over_while_the_app_starts_are_followed_once_it_runs()
    {
        var app = new AppDriver(queuedArguments: Arguments("--language=IL"));

        app.Frames.WaitUntil(() => app.AppState.Decompiler.Setup.Language == DecompilationLanguage.IL, Patience);

        Assert.Equal(DecompilationLanguage.IL, app.AppState.Decompiler.Setup.Language);
    }
}
