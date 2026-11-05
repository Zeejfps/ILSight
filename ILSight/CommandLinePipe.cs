using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using ZGF.Desktop;

namespace ILSight;

/// <summary>
/// How a later run of the app reaches the first. The first run listens for the command lines later
/// runs hand it; a later run hands its own over and lends the first run the right to come to the
/// front, so that one window serves them all. Only the user who started the first run can reach it.
/// </summary>
public sealed class CommandLinePipe : IDisposable
{
    private const int IntBytes = 4;
    private const int MaxArguments = 4096;
    private const int MaxArgumentBytes = 1 << 20;
    private const byte Received = 1;
    private static readonly TimeSpan HandOverTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BusyPipeWait = TimeSpan.FromMilliseconds(100);

    private readonly string _name;
    private readonly Func<CommandLine, bool> _tryHandle;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serving;

    private CommandLinePipe(string name, Func<CommandLine, bool> tryHandle)
    {
        _name = name;
        _tryHandle = tryHandle;
        _serving = Task.Run(ServeAsync);
    }

    /// <summary>Starts taking, in the background, the command lines later runs hand over to the pipe
    /// <paramref name="name"/>, giving them to <paramref name="tryHandle"/>. Once that returns false,
    /// later runs are refused and open windows of their own.</summary>
    public static CommandLinePipe Listen(string name, Func<CommandLine, bool> tryHandle)
    {
        return new CommandLinePipe(name, tryHandle);
    }

    /// <summary>Hands <paramref name="commandLine"/> to the run listening on the pipe <paramref name="name"/>
    /// and lends it the right to come to the front; false when it cannot be reached in time, and this
    /// run opens a window of its own. Waits on the thread pool, so a caller whose own thread runs the
    /// continuations is never blocked by them.</summary>
    public static bool TryHandOver(string name, CommandLine commandLine)
    {
        try
        {
            return Task.Run(() => HandOverAsync(name, commandLine)).GetAwaiter().GetResult();
        }
        catch (Exception e) when (IsBrokenHandOver(e))
        {
            return false;
        }
    }

    private static async Task<bool> HandOverAsync(string name, CommandLine commandLine)
    {
        using var timeout = new CancellationTokenSource(HandOverTimeout);
        var token = timeout.Token;
        await using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(token);
        var processId = await ReadIntAsync(pipe, token);
        ForegroundRights.LendTo(processId);
        await WriteArgumentsAsync(pipe, commandLine.ListHandedArguments(), token);
        var answer = await ReadByteAsync(pipe, token);
        return answer == Received;
    }

    private static bool IsBrokenHandOver(Exception e)
    {
        return e is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException or InvalidDataException;
    }

    private async Task ServeAsync()
    {
        var token = _stop.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await ServeOneAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (Exception e) when (IsBrokenHandOver(e))
            {
                await WaitForBusyPipe(token);
            }
        }
    }

    /// <summary>Takes the command line of the next run that connects, then tells it so. Hanging up
    /// without an answer tells a run that came too late to open a window of its own.</summary>
    private async Task ServeOneAsync(CancellationToken stop)
    {
        await using var pipe = new NamedPipeServerStream(_name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.WaitForConnectionAsync(stop);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
        timeout.CancelAfter(HandOverTimeout);
        var token = timeout.Token;
        await WriteIntAsync(pipe, Environment.ProcessId, token);
        var args = await ReadArgumentsAsync(pipe, token);
        if (!_tryHandle(CommandLine.Parse(args)))
            return;
        await pipe.WriteAsync(new[] { Received }, token);
        await pipe.FlushAsync(token);
    }

    /// <summary>Waits a moment after a connection broke, so that a pipe that stays busy is not tried in a tight loop.</summary>
    private static async Task WaitForBusyPipe(CancellationToken token)
    {
        try
        {
            await Task.Delay(BusyPipeWait, token);
        }
        catch (OperationCanceledException)
        {
        }
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
            throw new InvalidDataException("A handed command line is malformed.");
        if (count > max)
            throw new InvalidDataException("A handed command line is too long.");
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

    public void Dispose()
    {
        _stop.Cancel();
        _serving.Wait(HandOverTimeout);
        _stop.Dispose();
    }
}
