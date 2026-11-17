// Adapted from ILSpy's Processes/DiagnosticsIpcClient and DiagnosticsIpcMessage
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

namespace ILSight.Core.ProcessArea;

/// <summary>Where a runtime's diagnostics endpoint listens: a Unix domain socket on Linux and macOS, a
/// named pipe on Windows.</summary>
internal union DiagnosticsEndpoint(DiagnosticsEndpoint.UnixSocket, DiagnosticsEndpoint.NamedPipe)
{
    public sealed record UnixSocket(string Path);

    public sealed record NamedPipe(string Name);
}
