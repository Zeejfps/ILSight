using System.Threading.Channels;
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using ZGF.Desktop;
using ILSight.Commanding;

namespace ILSight;

/// <summary>
/// What a run and the receiver say to each other. A run sends its arguments and lends the receiver the
/// right to come to the front, so that one window serves them all. Only the user who started the
/// receiver can reach it.
/// </summary>
public static class RunArgumentsPipe
{
    private const int IntBytes = 4;
    private const int MaxArguments = 4096;
    private const int MaxArgumentBytes = 1 << 20;
    private const byte Received = 1;

    /// <summary>How long either side waits for the other.</summary>
    internal static readonly TimeSpan HandOverTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Hands <paramref name="arguments"/> to the receiver at <paramref name="address"/> and lends
    /// it the right to come to the front; false when it cannot be reached in time or refuses. Waits on
    /// the thread pool, so a caller whose own thread runs the continuations is never blocked by them.</summary>
    public static bool TrySend(RunArgumentsAddress address, RunArguments arguments)
    {
        try
        {
            using var timeout = new CancellationTokenSource(HandOverTimeout);
            var token = timeout.Token;
            return Task.Run(() => TrySendAsync(address, arguments, token)).GetAwaiter().GetResult();
        }
        catch (Exception e) when (IsBroken(e))
        {
            return false;
        }
    }

    private static async Task<bool> TrySendAsync(RunArgumentsAddress address, RunArguments arguments, CancellationToken token)
    {
        await using var pipe = new NamedPipeClientStream(".", address.Name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(token);
        var processId = await ReadIntAsync(pipe, token);
        ForegroundRights.LendTo(processId);
        await WriteArgumentsAsync(pipe, CommandLines.ListArguments(arguments), token);
        var answer = await ReadByteAsync(pipe, token);
        return answer == Received;
    }

    /// <summary>The arguments of the run connected to <paramref name="pipe"/>, once told where to lend the
    /// right to come to the front.</summary>
    internal static async Task<RunArguments> ReceiveAsync(Stream pipe, CancellationToken token)
    {
        await WriteIntAsync(pipe, Environment.ProcessId, token);
        var args = await ReadArgumentsAsync(pipe, token);
        return CommandLines.Parse(args).Arguments;
    }

    /// <summary>Tells the run connected to <paramref name="pipe"/> its arguments were taken. Hanging up
    /// without this tells it to open a window of its own.</summary>
    internal static async Task AcknowledgeAsync(Stream pipe, CancellationToken token)
    {
        await pipe.WriteAsync(new[] { Received }, token);
        await pipe.FlushAsync(token);
    }

    /// <summary>Whether <paramref name="e"/> only means the other side went away or sent nonsense.</summary>
    internal static bool IsBroken(Exception e)
    {
        return e is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException or InvalidDataException;
    }

    private static async Task WriteArgumentsAsync(Stream pipe, List<string> args, CancellationToken token)
    {
        await WriteIntAsync(pipe, args.Count, token);
        foreach (var arg in args)
        {
            var bytes = Encoding.UTF8.GetBytes(arg);
            await WriteIntAsync(pipe, bytes.Length, token);
            await pipe.WriteAsync(bytes, token);
        }
        await pipe.FlushAsync(token);
    }

    private static async Task<string[]> ReadArgumentsAsync(Stream pipe, CancellationToken token)
    {
        var count = await ReadCountAsync(pipe, MaxArguments, token);
        var args = new string[count];
        for (var i = 0; i < count; i++)
        {
            var length = await ReadCountAsync(pipe, MaxArgumentBytes, token);
            var bytes = new byte[length];
            await pipe.ReadExactlyAsync(bytes, token);
            args[i] = Encoding.UTF8.GetString(bytes);
        }
        return args;
    }

    /// <summary>A count read from <paramref name="pipe"/>, which no run sends past <paramref name="max"/>.</summary>
    private static async Task<int> ReadCountAsync(Stream pipe, int max, CancellationToken token)
    {
        var count = await ReadIntAsync(pipe, token);
        if (count < 0)
        {
            throw new InvalidDataException("Handed arguments are malformed.");
        }

        if (count > max)
        {
            throw new InvalidDataException("Handed arguments are too long.");
        }

        return count;
    }

    private static async Task WriteIntAsync(Stream pipe, int value, CancellationToken token)
    {
        var bytes = new byte[IntBytes];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        await pipe.WriteAsync(bytes, token);
        await pipe.FlushAsync(token);
    }

    private static async Task<int> ReadIntAsync(Stream pipe, CancellationToken token)
    {
        var bytes = new byte[IntBytes];
        await pipe.ReadExactlyAsync(bytes, token);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    private static async Task<byte> ReadByteAsync(Stream pipe, CancellationToken token)
    {
        var bytes = new byte[1];
        await pipe.ReadExactlyAsync(bytes, token);
        return bytes[0];
    }

    private static readonly TimeSpan RoleCheck = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan BrokenPipeWait = TimeSpan.FromMilliseconds(100);

    private static RunArgumentsReceiver StartReceiver(RunArgumentsAddress address, Action<RunArgumentsReceiver> run)
    {
        var receiver = new RunArgumentsReceiver { Name = address.Name, Role = new Mutex(false, address.Name) };
        var thread = new Thread(() => run(receiver)) { IsBackground = true, Name = "Run arguments" };
        receiver.Thread = thread;
        thread.Start();
        return receiver;
    }

    /// <summary>A receiver already listening when the role at <paramref name="address"/> is free; null
    /// when another run holds it. Never waits for the role.</summary>
    public static RunArgumentsReceiver? TryClaim(RunArgumentsAddress address)
    {
        var claimed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receiver = StartReceiver(address, r => RunArgumentsPipe.ClaimAndServe(r, claimed));
        var isClaimed = claimed.Task.GetAwaiter().GetResult();
        if (isClaimed)
        {
            return receiver;
        }

        receiver.Dispose();
        return null;
    }

    /// <summary>A receiver that takes the role at <paramref name="address"/> whenever it comes free.
    /// A run must only stand by once it will not send, or it could end up taking its own arguments.</summary>
    public static RunArgumentsReceiver StandBy(RunArgumentsAddress address)
    {
        return StartReceiver(address, static r => RunArgumentsPipe.WaitAndServe(r));
    }

    private static void ClaimAndServe(RunArgumentsReceiver receiver, TaskCompletionSource<bool> claimed)
    {
        var holdsRole = TryTakeRole(receiver, TimeSpan.Zero);
        claimed.SetResult(holdsRole);
        if (holdsRole)
        {
            ServeThenRelease(receiver);
        }
    }

    private static void WaitAndServe(RunArgumentsReceiver receiver)
    {
        while (!receiver.Stop.IsCancellationRequested)
        {
            if (TryTakeRole(receiver, RoleCheck))
            {
                ServeThenRelease(receiver);
                return;
            }
        }
    }

    private static bool TryTakeRole(RunArgumentsReceiver receiver, TimeSpan wait)
    {
        try
        {
            return receiver.Role.WaitOne(wait);
        }
        catch (AbandonedMutexException)
        {
            // The holder crashed, and the wait took the role all the same.
            return true;
        }
    }

    private static void ServeThenRelease(RunArgumentsReceiver receiver)
    {
        try
        {
            ServeAsync(receiver, receiver.Stop.Token).GetAwaiter().GetResult();
        }
        finally
        {
            receiver.Role.ReleaseMutex();
        }
    }

    /// <summary>Listens until stopped, on one server: replacing it for each run would drop the runs
    /// already waiting in its backlog.</summary>
    private static async Task ServeAsync(RunArgumentsReceiver receiver, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(receiver.Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await ServeRunsAsync(receiver, pipe, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (Exception e) when (RunArgumentsPipe.IsBroken(e))
            {
                await WaitForBrokenPipeAsync(token);
            }
        }
    }

    private static async Task ServeRunsAsync(RunArgumentsReceiver receiver, NamedPipeServerStream pipe, CancellationToken stop)
    {
        while (true)
        {
            await pipe.WaitForConnectionAsync(stop);
            await TakeArgumentsAsync(receiver, pipe, stop);
            pipe.Disconnect();
        }
    }

    /// <summary>Takes the arguments of the connected run, then tells it so. A run that goes away partway
    /// costs only its own arguments, and a closed inbox hangs up on it unanswered.</summary>
    private static async Task TakeArgumentsAsync(RunArgumentsReceiver receiver, Stream pipe, CancellationToken stop)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
        timeout.CancelAfter(RunArgumentsPipe.HandOverTimeout);
        try
        {
            var arguments = await RunArgumentsPipe.ReceiveAsync(pipe, timeout.Token);
            if (receiver.Inbox.Writer.TryWrite(arguments))
            {
                await RunArgumentsPipe.AcknowledgeAsync(pipe, timeout.Token);
            }
        }
        catch (Exception e) when (RunArgumentsPipe.IsBroken(e) && !stop.IsCancellationRequested)
        {
        }
    }

    /// <summary>Waits a moment after the server broke, so that one that stays broken is not retried in a tight loop.</summary>
    private static async Task WaitForBrokenPipeAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(BrokenPipeWait, token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Stops listening, then releases the role, which the next receiver can then take.</summary>
    public static void StopReceiver(RunArgumentsReceiver receiver)
    {
        receiver.Stop.Cancel();
        receiver.Thread?.Join();
        receiver.Stop.Dispose();
        receiver.Role.Dispose();
    }
}
