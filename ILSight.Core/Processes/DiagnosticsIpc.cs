// Adapted from ILSpy's Processes/DiagnosticsIpcClient and DiagnosticsIpcMessage
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;

namespace ILSight.Core.Processes;

/// <summary>Where a runtime's diagnostics endpoint listens: a Unix domain socket on Linux and macOS, a
/// named pipe on Windows.</summary>
internal abstract record DiagnosticsEndpoint
{
    private DiagnosticsEndpoint()
    {
    }

    public sealed record UnixSocket(string Path) : DiagnosticsEndpoint;

    public sealed record NamedPipe(string Name) : DiagnosticsEndpoint;
}

/// <summary>How a runtime answered one command: with the command's payload, or by refusing it with an
/// HRESULT, as an older runtime refuses a command it doesn't know.</summary>
internal abstract record IpcResponse
{
    private IpcResponse()
    {
    }

    public sealed record Ok(byte[] Payload) : IpcResponse;

    public sealed record Rejected(int HResult) : IpcResponse;
}

/// <summary>
/// The CoreCLR diagnostics IPC protocol (dotnet/diagnostics, documentation/design-docs/ipc-protocol.md),
/// spoken to one runtime. A message is a 20-byte header (the 14-byte magic <c>DOTNET_IPC_V1\0</c>, a u16
/// total size, a u8 command set, a u8 command id, a reserved u16) and a payload. Strings are UTF-16LE with
/// a u32 count of chars that includes the terminator, and integers are little endian. Every command opens
/// a connection of its own, as the protocol takes one command per connection, and runs under a budget, so
/// that a runtime that never answers can't hold up the caller.
/// </summary>
/// <remarks>What the transports and the stream throw is left to <see cref="RunningProcesses"/>, the seam
/// that turns it into a result.</remarks>
internal static class DiagnosticsIpc
{
    public const int HeaderSize = 20;

    private const byte ServerCommandSet = 0xFF;
    private const byte ServerOkCommandId = 0x00;

    private const byte ProcessCommandSet = 0x04;
    private const byte ProcessInfoCommandId = 0x00;
    private const byte ProcessInfo2CommandId = 0x04;

    private const byte EventPipeCommandSet = 0x02;
    private const byte StopTracingCommandId = 0x01;
    private const byte CollectTracing2CommandId = 0x03;
    private const byte CollectTracing4CommandId = 0x05;

    /// <summary>The rundown events the runtime sends as the session stops: the loader's, sent at the end.
    /// The runtime's default, 0x80020139, also asks for the JIT, NGen and IL-to-native rundown, which in a
    /// process that has run a while outweighs the module list by orders of magnitude and can overrun the
    /// session's buffer, losing the loader events this is for.</summary>
    private const ulong LoaderRundownKeyword = 0x80000108;

    private const string RuntimeProviderName = "Microsoft-Windows-DotNETRuntime";
    private const ulong LoaderKeyword = 0x8;
    private const uint InformationalLevel = 4;
    private const uint NetTraceFormat = 1;

    /// <summary>The session carries only loader events and the rundown, so a small buffer is ample; a
    /// larger one would have the target reserve memory for nothing.</summary>
    private const uint CircularBufferMB = 16;

    /// <summary>A string longer than this is no command line or version, so the response is corrupt, and
    /// isn't allowed to allocate without bound.</summary>
    private const int MaxStringLength = 1024 * 1024;

    public const string TransportPrefix = "dotnet-diagnostic-";

    /// <summary>How long a status query may take. A healthy runtime answers in milliseconds; what this
    /// waits out is one that never will, as one suspended on a startup diagnostic port, so a tighter budget
    /// only turns a busy machine into processes listed without what they say of themselves.</summary>
    public static readonly TimeSpan CommandBudget = TimeSpan.FromSeconds(10);

    /// <summary>How long a rundown may take: it walks every loaded module, more slowly in a large process.</summary>
    public static readonly TimeSpan RundownBudget = TimeSpan.FromSeconds(30);

    private static ReadOnlySpan<byte> Magic => "DOTNET_IPC_V1\0"u8;

    public static byte[] EncodeRequest(byte commandSet, byte commandId, ReadOnlySpan<byte> payload)
    {
        var message = new byte[HeaderSize + payload.Length];
        Magic.CopyTo(message);
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(14), checked((ushort)(HeaderSize + payload.Length)));
        message[16] = commandSet;
        message[17] = commandId;
        // Bytes 18 and 19 are the reserved field, and stay zero.
        payload.CopyTo(message.AsSpan(HeaderSize));
        return message;
    }

    public static void WriteString(BinaryWriter writer, string? value)
    {
        if (value is null)
        {
            writer.Write(0u);
            return;
        }
        writer.Write((uint)(value.Length + 1));
        foreach (var c in value)
        {
            writer.Write((ushort)c);
        }

        writer.Write((ushort)0);
    }

    /// <summary>A string as the protocol writes it; null for one of no length.</summary>
    /// <exception cref="IOException">The length is past any plausible string's.</exception>
    /// <exception cref="EndOfStreamException">The payload ends inside the string.</exception>
    public static string? ReadString(BinaryReader reader)
    {
        var length = reader.ReadUInt32();
        if (length == 0)
        {
            return null;
        }

        if (length > MaxStringLength)
        {
            throw new IOException($"A diagnostics IPC string claims {length} chars.");
        }

        var chars = new char[length];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)reader.ReadUInt16();
        }

        var end = chars.Length;
        if (chars[end - 1] == '\0')
        {
            end--;
        }

        return new string(chars, 0, end);
    }

    /// <summary>One response, read off <paramref name="stream"/>. For a streaming command, as EventPipe's,
    /// what follows the sized message stays in the stream for the caller.</summary>
    /// <exception cref="IOException">What came back is no response of the protocol.</exception>
    public static async Task<IpcResponse> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderSize];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        if (!header.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new IOException("A diagnostics IPC response does not start with the DOTNET_IPC_V1 magic.");
        }

        var size = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(14));
        if (size < HeaderSize)
        {
            throw new IOException($"A diagnostics IPC response claims an impossible size of {size} bytes.");
        }

        var payload = new byte[size - HeaderSize];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);

        if (header[16] == ServerCommandSet && header[17] == ServerOkCommandId)
        {
            return new IpcResponse.Ok(payload);
        }

        return new IpcResponse.Rejected(payload.Length >= 4 ? BinaryPrimitives.ReadInt32LittleEndian(payload) : 0);
    }

    /// <summary>What the runtime behind <paramref name="endpoint"/> says of itself. ProcessInfo2 is asked
    /// first, which names the entry assembly and the runtime's version, and the original ProcessInfo, which
    /// works from .NET Core 3.0 on, when the runtime is older than .NET 6 and refuses it.</summary>
    /// <exception cref="TimeoutException">The runtime didn't answer within <paramref name="budget"/>.</exception>
    /// <exception cref="IOException">The runtime refused both, or its answer could not be read.</exception>
    public static async Task<ProcessInfo> QueryProcessInfoAsync(DiagnosticsEndpoint endpoint, TimeSpan budget, CancellationToken cancellationToken)
    {
        var info = await QueryProcessInfoAsync(endpoint, ProcessInfo2CommandId, budget, cancellationToken).ConfigureAwait(false);
        if (info is not null)
        {
            return info;
        }

        info = await QueryProcessInfoAsync(endpoint, ProcessInfoCommandId, budget, cancellationToken).ConfigureAwait(false);
        if (info is null)
        {
            throw new IOException("The runtime refused to say what process it is.");
        }

        return info;
    }

    /// <summary>The answer to one of the ProcessInfo commands, or null when the runtime refused it.</summary>
    private static Task<ProcessInfo?> QueryProcessInfoAsync(DiagnosticsEndpoint endpoint, byte commandId, TimeSpan budget, CancellationToken cancellationToken)
    {
        return RunWithinAsync(budget, cancellationToken, async token =>
        {
            var stream = await ConnectAsync(endpoint, token).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(EncodeRequest(ProcessCommandSet, commandId, ReadOnlySpan<byte>.Empty), token).ConfigureAwait(false);
                var response = await ReadResponseAsync(stream, token).ConfigureAwait(false);
                if (response is not IpcResponse.Ok ok)
                {
                    return null;
                }

                using var reader = new BinaryReader(new MemoryStream(ok.Payload));
                reader.ReadInt64(); // the process id, which the caller knows
                reader.ReadBytes(16); // the runtime's cookie
                var commandLine = ReadString(reader);
                ReadString(reader); // the operating system
                var architecture = ReadString(reader);
                string? entryAssembly = null;
                string? runtimeVersion = null;
                if (commandId == ProcessInfo2CommandId)
                {
                    entryAssembly = ReadString(reader);
                    runtimeVersion = ReadString(reader);
                }
                return new ProcessInfo
                {
                    CommandLine = commandLine,
                    Architecture = architecture,
                    EntryAssemblyName = entryAssembly,
                    RuntimeVersion = runtimeVersion,
                };
            }
        });
    }

    /// <summary>
    /// The nettrace stream of a session run only for its rundown, positioned at its start: the runtime sends
    /// an event for each module and assembly it has loaded as the session stops. CollectTracing4 is asked
    /// first, which picks only the loader's rundown, and CollectTracing2, which sends the whole rundown, when
    /// the runtime is too old to know it.
    /// </summary>
    /// <exception cref="TimeoutException">The runtime didn't finish within <paramref name="budget"/>.</exception>
    /// <exception cref="IOException">The runtime refused both, or the session failed.</exception>
    public static async Task<MemoryStream> CollectRundownAsync(DiagnosticsEndpoint endpoint, TimeSpan budget, CancellationToken cancellationToken)
    {
        var trace = await CollectAsync(endpoint, CollectTracing4CommandId, budget, cancellationToken).ConfigureAwait(false);
        if (trace is not null)
        {
            return trace;
        }

        trace = await CollectAsync(endpoint, CollectTracing2CommandId, budget, cancellationToken).ConfigureAwait(false);
        if (trace is null)
        {
            throw new IOException("The runtime refused to start a session.");
        }

        return trace;
    }

    /// <summary>The trace of one session, or null when the runtime refused the command that starts it.</summary>
    private static Task<MemoryStream?> CollectAsync(DiagnosticsEndpoint endpoint, byte commandId, TimeSpan budget, CancellationToken cancellationToken)
    {
        return RunWithinAsync(budget, cancellationToken, async token =>
        {
            var session = await ConnectAsync(endpoint, token).ConfigureAwait(false);
            await using (session.ConfigureAwait(false))
            {
                await session.WriteAsync(EncodeRequest(EventPipeCommandSet, commandId, EncodeCollectTracing(commandId)), token).ConfigureAwait(false);
                var response = await ReadResponseAsync(session, token).ConfigureAwait(false);
                if (response is not IpcResponse.Ok ok)
                {
                    return null;
                }

                if (ok.Payload.Length < sizeof(ulong))
                {
                    throw new IOException("The runtime did not give the session's id.");
                }

                var sessionId = BinaryPrimitives.ReadUInt64LittleEndian(ok.Payload);

                // The runtime writes the rundown into this same connection as the session stops, so it is read
                // while the stop is asked for: left unread, a full buffer would keep the stop from finishing.
                var trace = new MemoryStream();
                var drain = session.CopyToAsync(trace, token);
                try
                {
                    await StopTracingAsync(endpoint, sessionId, token).ConfigureAwait(false);
                    await drain.ConfigureAwait(false);
                }
                catch (Exception e) when (e is IOException or SocketException or TimeoutException or OperationCanceledException or UnauthorizedAccessException)
                {
                    await AbandonCollectionAsync(session, drain, trace).ConfigureAwait(false);
                    throw;
                }
                trace.Position = 0;
                return trace;
            }
        });
    }

    /// <summary>
    /// Winds up a collection that failed partway, in the one order that works. The connection goes first,
    /// as the drain waits on a read of it that nothing else will end now. Then the drain, whose own failure
    /// only echoes the one on its way to the caller, but must be awaited all the same: a faulted task nobody
    /// awaited is raised again by the finalizer, minutes later and out of context. The half-read trace goes
    /// last, once nothing writes to it.
    /// </summary>
    internal static async Task AbandonCollectionAsync(Stream session, Task drain, MemoryStream trace)
    {
        await session.DisposeAsync().ConfigureAwait(false);
        try
        {
            await drain.ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
        {
            // Dropped: this is how a read ends when its connection is closed under it, which was just done,
            // and the failure that made it so is already on its way to the caller.
        }
        await trace.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task StopTracingAsync(DiagnosticsEndpoint endpoint, ulong sessionId, CancellationToken cancellationToken)
    {
        var stream = await ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var payload = new byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(payload, sessionId);
            await stream.WriteAsync(EncodeRequest(EventPipeCommandSet, StopTracingCommandId, payload), cancellationToken).ConfigureAwait(false);
            var response = await ReadResponseAsync(stream, cancellationToken).ConfigureAwait(false);
            if (response is IpcResponse.Rejected rejected)
            {
                throw new IOException($"The runtime refused to stop the session (error 0x{rejected.HResult:X8}).");
            }
        }
    }

    private static byte[] EncodeCollectTracing(byte commandId)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.Unicode, leaveOpen: true))
        {
            writer.Write(CircularBufferMB);
            writer.Write(NetTraceFormat);
            // The rundown is what makes this a list of all that is loaded, not of what loads from now on.
            if (commandId == CollectTracing4CommandId)
            {
                writer.Write(LoaderRundownKeyword);
                writer.Write(false); // no stack walks, which are only overhead here
            }
            else
            {
                writer.Write(true);
            }
            writer.Write(1u); // one provider follows
            writer.Write(LoaderKeyword);
            writer.Write(InformationalLevel);
            WriteString(writer, RuntimeProviderName);
            WriteString(writer, null); // no filter
        }
        return buffer.ToArray();
    }

    /// <summary>Runs <paramref name="command"/> under <paramref name="budget"/>, and turns the budget running
    /// out into a <see cref="TimeoutException"/>. That keeps the two apart: the caller cancelling is the
    /// caller's business, while a runtime that never answered is a fact about the process.</summary>
    private static async Task<T> RunWithinAsync<T>(TimeSpan budget, CancellationToken cancellationToken, Func<CancellationToken, Task<T>> command)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(budget);
        try
        {
            return await command(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The diagnostics endpoint did not answer within {budget.TotalSeconds:0.###} seconds.");
        }
    }

    private static async Task<Stream> ConnectAsync(DiagnosticsEndpoint endpoint, CancellationToken cancellationToken)
    {
        switch (endpoint)
        {
            case DiagnosticsEndpoint.NamedPipe pipeEndpoint:
            {
                var pipe = new NamedPipeClientStream(".", pipeEndpoint.Name, PipeDirection.InOut, PipeOptions.Asynchronous);
                var connected = false;
                try
                {
                    await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    connected = true;
                    return pipe;
                }
                finally
                {
                    if (!connected)
                        {
                            await pipe.DisposeAsync().ConfigureAwait(false);
                        }
                    }
            }
            case DiagnosticsEndpoint.UnixSocket socketEndpoint:
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                var connected = false;
                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketEndpoint.Path), cancellationToken).ConfigureAwait(false);
                    var stream = new NetworkStream(socket, ownsSocket: true);
                    connected = true;
                    return stream;
                }
                finally
                {
                    if (!connected)
                        {
                            socket.Dispose();
                        }
                    }
            }
            default:
                throw new InvalidOperationException($"Unknown endpoint {endpoint}.");
        }
    }
}
