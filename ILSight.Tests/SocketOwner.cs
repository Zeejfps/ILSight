using System.Net.Sockets;

namespace ILSight.Tests;

/// <summary>A listening socket and the connection it accepts, closed together.</summary>
internal sealed class SocketOwner(Socket listener, Task<Socket> accept) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        if (accept.IsCompletedSuccessfully)
        {
            var accepted = await accept;
            accepted.Dispose();
        }

        listener.Dispose();
    }
}
