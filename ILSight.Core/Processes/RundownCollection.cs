// Adapted from ILSpy's Processes/DiagnosticsIpcClient
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

using System.Net.Sockets;

namespace ILSight.Core.Processes;

/// <summary>
/// One rundown being read: the session's connection, the drain reading what the runtime writes into it,
/// and the trace it is read into, until the trace is taken. Disposing it winds the collection up in the one
/// order that works, however the collection ended: the connection goes first, as the drain waits on a read
/// of it that nothing else will end; then the drain, which must be awaited all the same (a faulted task
/// nobody awaited is raised again by the finalizer, minutes later and out of context); then a trace nobody
/// took, once nothing writes to it.
/// </summary>
internal sealed class RundownCollection : IAsyncDisposable
{
    private readonly Stream _session;
    private readonly Task _drain;
    private MemoryStream? _trace;

    private RundownCollection(Stream session, MemoryStream trace, Task drain)
    {
        _session = session;
        _trace = trace;
        _drain = drain;
    }

    /// <summary>What ended the drain when the collection was wound up before the rundown ended: the echo of
    /// closing the connection under its read, whose cause is already on its way to the caller. Null when the
    /// drain ended on its own.</summary>
    public Exception? DrainEnding { get; private set; }

    /// <summary>Starts reading what the runtime writes into <paramref name="session"/>. The runtime writes the
    /// rundown into this same connection as the session stops, so it is read while the stop is asked for: left
    /// unread, a full buffer would keep the stop from finishing.</summary>
    public static RundownCollection Start(Stream session, CancellationToken cancellationToken)
    {
        var trace = new MemoryStream();
        return new RundownCollection(session, trace, session.CopyToAsync(trace, cancellationToken));
    }

    /// <summary>Waits for the runtime to end the rundown, and hands out the trace, positioned at its start and
    /// now the caller's to dispose.</summary>
    public async Task<MemoryStream> FinishAsync(CancellationToken cancellationToken)
    {
        await _drain.WaitAsync(cancellationToken).ConfigureAwait(false);
        var trace = _trace ?? throw new InvalidOperationException("The rundown's trace was taken already.");
        _trace = null;
        trace.Position = 0;
        return trace;
    }

    public async ValueTask DisposeAsync()
    {
        await _session.DisposeAsync().ConfigureAwait(false);
        try
        {
            await _drain.ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            DrainEnding = ex;
        }
        catch (SocketException ex)
        {
            DrainEnding = ex;
        }
        catch (ObjectDisposedException ex)
        {
            DrainEnding = ex;
        }
        catch (OperationCanceledException ex)
        {
            DrainEnding = ex;
        }

        var trace = _trace;
        _trace = null;
        if (trace is not null)
        {
            await trace.DisposeAsync().ConfigureAwait(false);
        }
    }
}
