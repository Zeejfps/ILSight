using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using ZGF.Desktop;

namespace ILSight;

/// <summary>
/// What a run and the receiver say to each other. A run sends its command and lends the receiver the
/// right to come to the front, so that one window serves them all. Only the user who started the
/// receiver can reach it.
/// </summary>
public static class ExternalCommandPipe
{
    private const int IntBytes = 4;
    private const int MaxArguments = 4096;
    private const int MaxArgumentBytes = 1 << 20;
    private const byte Received = 1;

    /// <summary>How long either side waits for the other.</summary>
    internal static readonly TimeSpan HandOverTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Hands <paramref name="command"/> to the receiver at <paramref name="address"/> and lends
    /// it the right to come to the front; false when it cannot be reached in time or refuses. Waits on
    /// the thread pool, so a caller whose own thread runs the continuations is never blocked by them.</summary>
    public static bool TrySend(ExternalCommandAddress address, ExternalCommand command)
    {
        try
        {
            return Task.Run(() => SendAsync(address, command)).GetAwaiter().GetResult();
        }
        catch (Exception e) when (IsBroken(e))
        {
            return false;
        }
    }

    private static async Task<bool> SendAsync(ExternalCommandAddress address, ExternalCommand command)
    {
        using var timeout = new CancellationTokenSource(HandOverTimeout);
        var token = timeout.Token;
        await using var pipe = new NamedPipeClientStream(".", address.Name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(token);
        var processId = await ReadIntAsync(pipe, token);
        ForegroundRights.LendTo(processId);
        await WriteArgumentsAsync(pipe, CommandLine.ListArguments(command), token);
        var answer = await ReadByteAsync(pipe, token);
        return answer == Received;
    }

    /// <summary>The command of the run connected to <paramref name="pipe"/>, once told where to lend the
    /// right to come to the front.</summary>
    internal static async Task<ExternalCommand> ReceiveAsync(Stream pipe, CancellationToken token)
    {
        await WriteIntAsync(pipe, Environment.ProcessId, token);
        var args = await ReadArgumentsAsync(pipe, token);
        return CommandLine.Parse(args).Command;
    }

    /// <summary>Tells the run connected to <paramref name="pipe"/> its command was taken. Hanging up
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
            throw new InvalidDataException("A handed command is malformed.");
        if (count > max)
            throw new InvalidDataException("A handed command is too long.");
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
}
