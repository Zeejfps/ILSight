// Adapted from ILSpy's Processes/ProcessExplorer, DiagnosticsPortScanner and RunningDotNetProcess
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

namespace ILSight.Core.Processes;

/// <summary>A module a process has loaded: one whose file can be opened, or one with no file to open, loaded
/// from bytes, made at run time, or in a file this user can't see.</summary>
public abstract record RunningModule
{
    private RunningModule()
    {
    }

    /// <summary>The file name of a module on disk; the assembly's simple name of one in memory.</summary>
    public abstract string Name { get; init; }

    public sealed record OnDisk(string Name, AssemblyPath Path) : RunningModule;

    public sealed record InMemory(string Name) : RunningModule;
}
