// Adapted from ILSpy's Processes/DiagnosticsIpcClient and DiagnosticsIpcMessage
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

using System.Diagnostics;

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

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<UnixSocket, TResult> unixSocket, Func<NamedPipe, TResult> namedPipe)
    {
        return this switch
        {
            UnixSocket c => unixSocket(c),
            NamedPipe c => namedPipe(c),
            _ => throw new UnreachableException($"DiagnosticsEndpoint.Match does not handle {GetType().Name}."),
        };
    }
}
