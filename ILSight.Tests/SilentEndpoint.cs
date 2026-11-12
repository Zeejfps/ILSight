using System.IO.Pipes;
using System.Net.Sockets;
using ILSight.Core.Processes;

namespace ILSight.Tests;

/// <summary>An endpoint that takes connections and never answers, as a runtime suspended at startup does.</summary>
internal sealed class SilentEndpoint : IAsyncDisposable
{
    private readonly IAsyncDisposable _server;
    private readonly Task _accepted;

    private SilentEndpoint(DiagnosticsEndpoint endpoint, IAsyncDisposable server, Task accepted)
    {
        Endpoint = endpoint;
        _server = server;
        _accepted = accepted;
    }

    public DiagnosticsEndpoint Endpoint { get; }

    public static Task<SilentEndpoint> StartAsync(TempDir temp)
    {
        if (OperatingSystem.IsWindows())
        {
            var name = $"ilsight-silent-{Guid.NewGuid():N}";
            var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            var accepted = pipe.WaitForConnectionAsync();
            return Task.FromResult(new SilentEndpoint(new DiagnosticsEndpoint.NamedPipe(name), pipe, accepted));
        }

        Directory.CreateDirectory(temp.Path);
        var path = temp.PathOf("s.sock");
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(path));
        socket.Listen();
        var accept = socket.AcceptAsync();
        return Task.FromResult(new SilentEndpoint(new DiagnosticsEndpoint.UnixSocket(path), new SocketOwner(socket, accept), accept));
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
        try
        {
            await _accepted;
        }
        catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
        {
            // The listener closed before or after a connection came; either is the end of the test.
        }
    }
}
