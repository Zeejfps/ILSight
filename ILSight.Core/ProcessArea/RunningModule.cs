// Adapted from ILSpy's Processes/ProcessExplorer, DiagnosticsPortScanner and RunningDotNetProcess
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

namespace ILSight.Core.ProcessArea;

/// <summary>A module a process has loaded: one whose file can be opened, or one with no file to open, loaded
/// from bytes, made at run time, or in a file this user can't see.</summary>
public union RunningModule(RunningModule.OnDisk, RunningModule.InMemory)
{
    public sealed record OnDisk
    {
        /// <summary>The module's file name.</summary>
        public required string Name { get; init; }

        public required AssemblyPath Path { get; init; }
    }

    public sealed record InMemory
    {
        /// <summary>The assembly's simple name.</summary>
        public required string Name { get; init; }
    }

    /// <summary>The file name of a module on disk; the assembly's simple name of one in memory.</summary>
    public string Name => this switch
    {
        OnDisk onDisk => onDisk.Name,
        InMemory inMemory => inMemory.Name,
    };
}
