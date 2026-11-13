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
    private const byte Received = 1;

    /// <summary>How long either side waits for the other.</summary>
    internal static readonly TimeSpan HandOverTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Hands <paramref name="arguments"/> to the receiver at <paramref name="address"/> and lends it the right to
    /// come to the front; whether it took them. What the pipe throws goes through, for the caller to translate.</summary>
    internal static async Task<bool> TrySendAsync(RunArgumentsAddress address, RunArguments arguments, CancellationToken token)
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

    private static readonly TimeSpan BrokenPipeWait = TimeSpan.FromMilliseconds(100);

    /// <summary>Listens at <paramref name="name"/> until stopped, on one server, putting the arguments each run sends in
    /// <paramref name="inbox"/>: replacing the server for each run would drop the runs already waiting in its
    /// backlog.</summary>
    internal static async Task ServeAsync(string name, Channel<RunArguments> inbox, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await ServeRunsAsync(inbox, pipe, token);
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

    private static async Task ServeRunsAsync(Channel<RunArguments> inbox, NamedPipeServerStream pipe, CancellationToken stop)
    {
        while (true)
        {
            await pipe.WaitForConnectionAsync(stop);
            await TakeArgumentsAsync(inbox, pipe, stop);
            pipe.Disconnect();
        }
    }

    /// <summary>Takes the arguments of the connected run, then tells it so. A run that goes away partway
    /// costs only its own arguments, and a closed inbox hangs up on it unanswered.</summary>
    private static async Task TakeArgumentsAsync(Channel<RunArguments> inbox, Stream pipe, CancellationToken stop)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
        timeout.CancelAfter(RunArgumentsPipe.HandOverTimeout);
        var arguments = await RunArgumentsPipe.TryReceiveAsync(pipe, stop, timeout.Token);
        if (arguments is null)
        {
            return;
        }

        if (!inbox.Writer.TryWrite(arguments))
        {
            return;
        }

        // A run that gets no acknowledgement, or a broken one, opens a window of its own: nothing more follows here.
        await RunArgumentsPipe.AcknowledgeAsync(pipe, timeout.Token)
            .ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
    }

    /// <summary>The arguments of the run connected to <paramref name="pipe"/>; null when it went away, sent nonsense or
    /// took too long (<paramref name="timeout"/>), which costs only its own arguments. Stopping
    /// (<paramref name="stop"/>) goes through.</summary>
    private static async Task<RunArguments?> TryReceiveAsync(Stream pipe, CancellationToken stop, CancellationToken timeout)
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

    /// <summary>Waits a moment after the server broke, so that one that stays broken is not retried in a tight loop;
    /// stopping cuts it short, which the serving loop sees for itself.</summary>
    private static async Task WaitForBrokenPipeAsync(CancellationToken token)
    {
        await Task.Delay(BrokenPipeWait, token).ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
    }
}
