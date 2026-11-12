// Adapted from ILSpy's Processes/ProcessExplorer, DiagnosticsPortScanner and RunningDotNetProcess
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

namespace ILSight.Core.Processes;

/// <summary>The id the operating system gives a process.</summary>
public readonly record struct ProcessId(int Value)
{
    public override string ToString()
    {
        return Value.ToString();
    }
}
