using System.Threading.Channels;
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using ZGF.Desktop;
using ILSight.CommandArea;

namespace ILSight.ILSightAppArea;

/// <summary>
/// What a run and the receiver say to each other. A run sends its arguments and lends the receiver the
/// right to come to the front, so that one window serves them all. Only the user who started the
/// receiver can reach it. The receiver (<see cref="RunArgumentsReceiver"/>) holds the role and its thread; this is the
/// wire and the serving loop that thread runs over its inbox.
/// </summary>
public static class RunArgumentsPipe
{
    private const int IntBytes = 4;
    private const int MaxArguments = 4096;
    private const int MaxArgumentBytes = 1 << 20;
    private const byte Refused = 0;
    private const byte Received = 1;
    private const int RequestIdBytes = 16;

    /// <summary>How long either side waits for the other.</summary>
    internal static readonly TimeSpan HandOverTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The end a run sends its arguments through to the receiver at <paramref name="address"/>.</summary>
    internal static NamedPipeClientStream CreateClient(RunArgumentsAddress address)
    {
        return new NamedPipeClientStream(".", address.Name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }

    /// <summary>Connects <paramref name="pipe"/> to the receiver and lends it the right to come to the front; nothing of
    /// the arguments is sent yet. What the pipe throws goes through, for the caller to translate.</summary>
    internal static async Task ReachAsync(NamedPipeClientStream pipe, CancellationToken token)
    {
        await pipe.ConnectAsync(token);
        var processId = await ReadIntAsync(pipe, token);
        ForegroundRights.LendTo(processId);
    }

    /// <summary>Sends <paramref name="handed"/> to the receiver <paramref name="pipe"/> reached; whether it took them
    /// (false: it refused them). What the pipe throws goes through, for the caller to translate.</summary>
    internal static async Task<bool> TryHandOverAsync(Stream pipe, HandedArguments handed, CancellationToken token)
    {
        await pipe.WriteAsync(handed.RequestId.ToByteArray(), token);
        await WriteArgumentsAsync(pipe, CommandLines.ListArguments(handed.Arguments), token);
        var answer = await ReadByteAsync(pipe, token);
        return answer == Received;
    }

    /// <summary>The arguments of the run connected to <paramref name="pipe"/>, and the request that carried them, once
    /// told where to lend the right to come to the front.</summary>
    internal static async Task<HandedArguments> ReceiveAsync(Stream pipe, CancellationToken token)
    {
        await WriteIntAsync(pipe, Environment.ProcessId, token);
        var id = new byte[RequestIdBytes];
        await pipe.ReadExactlyAsync(id, token);
        var args = await ReadArgumentsAsync(pipe, token);
        return new HandedArguments { RequestId = new Guid(id), Arguments = CommandLines.Parse(args).Arguments };
    }

    /// <summary>Tells the run connected to <paramref name="pipe"/> its arguments were taken, now or by an earlier send of
    /// the same request.</summary>
    internal static async Task AcknowledgeAsync(Stream pipe, CancellationToken token)
    {
        await pipe.WriteAsync(new[] { Received }, token);
        await pipe.FlushAsync(token);
    }

    /// <summary>Tells the run connected to <paramref name="pipe"/> its arguments were refused (the app is closing), for it
    /// to open a window of its own.</summary>
    internal static async Task RefuseAsync(Stream pipe, CancellationToken token)
    {
        await pipe.WriteAsync(new[] { Refused }, token);
        await pipe.FlushAsync(token);
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

    private static readonly TimeSpan BrokenPipeWait = TimeSpan.FromMilliseconds(100);

    /// <summary>Listens at <paramref name="name"/> until stopped, on one server, putting the arguments each run sends in
    /// <paramref name="inbox"/>, each request once however often it is sent: replacing the server for each run would
    /// drop the runs already waiting in its backlog.</summary>
    internal static async Task ServeAsync(string name, Channel<RunArguments> inbox, CancellationToken token)
    {
        var taken = new HashSet<Guid>();
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await ServeRunsAsync(inbox, taken, pipe, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (IOException)
            {
                await WaitForBrokenPipeAsync(token);
            }
            catch (TimeoutException)
            {
                await WaitForBrokenPipeAsync(token);
            }
            catch (OperationCanceledException)
            {
                await WaitForBrokenPipeAsync(token);
            }
            catch (InvalidDataException)
            {
                await WaitForBrokenPipeAsync(token);
            }
        }
    }

    private static async Task ServeRunsAsync(Channel<RunArguments> inbox, HashSet<Guid> taken, NamedPipeServerStream pipe, CancellationToken stop)
    {
        while (true)
        {
            await pipe.WaitForConnectionAsync(stop);
            await TakeArgumentsAsync(inbox, taken, pipe, stop);
            pipe.Disconnect();
        }
    }

    // A run that goes away partway, sends nonsense or takes too long costs only its own arguments.
    private static async Task TakeArgumentsAsync(Channel<RunArguments> inbox, HashSet<Guid> taken, Stream pipe, CancellationToken stop)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
        timeout.CancelAfter(RunArgumentsPipe.HandOverTimeout);
        var handed = await RunArgumentsPipe.TryReceiveAsync(pipe, stop, timeout.Token);
        if (handed is null)
        {
            return;
        }

        if (!TryTake(inbox, taken, handed))
        {
            await RunArgumentsPipe.RefuseAsync(pipe, timeout.Token)
                .ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
            return;
        }

        // Written to the inbox before acknowledged: a run whose acknowledgement is lost sends again under the same
        // request, which is acknowledged then without being taken twice. Nothing more follows here.
        await RunArgumentsPipe.AcknowledgeAsync(pipe, timeout.Token)
            .ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
    }

    private static bool TryTake(Channel<RunArguments> inbox, HashSet<Guid> taken, HandedArguments handed)
    {
        if (taken.Contains(handed.RequestId))
        {
            return true;
        }

        if (!inbox.Writer.TryWrite(handed.Arguments))
        {
            return false;
        }

        taken.Add(handed.RequestId);
        return true;
    }

    private static async Task<HandedArguments?> TryReceiveAsync(Stream pipe, CancellationToken stop, CancellationToken timeout)
    {
        try
        {
            return await RunArgumentsPipe.ReceiveAsync(pipe, timeout);
        }
        catch (IOException)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
        {
            return null;
        }
    }

    // So that a server that stays broken is not retried in a tight loop; stopping cuts it short, which the serving loop
    // sees for itself.
    private static async Task WaitForBrokenPipeAsync(CancellationToken token)
    {
        await Task.Delay(BrokenPipeWait, token).ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
    }
}
