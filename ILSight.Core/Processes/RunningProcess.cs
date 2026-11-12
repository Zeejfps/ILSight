// Adapted from ILSpy's Processes/ProcessExplorer, DiagnosticsPortScanner and RunningDotNetProcess
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

namespace ILSight.Core.Processes;

/// <summary>A running .NET process, found by its diagnostics endpoint: its id and name, and what its runtime
/// said of itself, or null when it didn't answer, as one suspended at startup or shutting down doesn't.</summary>
public sealed record RunningProcess(ProcessId Id, string Name, ProcessInfo? Info);
