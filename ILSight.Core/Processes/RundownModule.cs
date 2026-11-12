// Adapted from ILSpy's Processes/NettraceRundownReader
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

namespace ILSight.Core.Processes;

/// <summary>A module as a runtime's rundown reports it: the paths it gives, either of which may be empty,
/// and the simple name of the assembly that holds it, when the rundown named that too.</summary>
internal sealed record RundownModule
{
    public required string IlPath { get; init; }

    public required string NativePath { get; init; }

    public required string? AssemblyName { get; init; }
}
