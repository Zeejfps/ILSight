using System.Net.Sockets;

namespace ILSight.Tests;

/// <summary>A listening socket and the connection it accepts, closed together.</summary>
internal sealed class SocketOwner(Socket listener, Task<Socket> accept) : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        if (accept.IsCompletedSuccessfully)
        {
            accept.Result.Dispose();
        }

        listener.Dispose();
        return ValueTask.CompletedTask;
    }
}
