// Adapted from ILSpy's Processes/ProcessExplorer, DiagnosticsPortScanner and RunningDotNetProcess
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

using System.Diagnostics;

namespace ILSight.Core.Processes;

/// <summary>A module a process has loaded: one whose file can be opened, or one with no file to open, loaded
/// from bytes, made at run time, or in a file this user can't see.</summary>
public abstract record RunningModule
{
    private RunningModule()
    {
    }

    /// <summary>The file name of a module on disk; the assembly's simple name of one in memory.</summary>
    public required string Name { get; init; }

    public sealed record OnDisk : RunningModule
    {
        public required AssemblyPath Path { get; init; }
    }

    public sealed record InMemory : RunningModule;

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which
    /// it is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, OnDisk> onDisk, Action<TState, InMemory> inMemory)
    {
        switch (this)
        {
            case OnDisk c:
                onDisk(state, c);
                return;
            case InMemory c:
                inMemory(state, c);
                return;
            default:
                throw new UnreachableException($"RunningModule.Switch does not handle {GetType().Name}.");
        }
    }
}
