using ILSight.Core;

namespace ILSight;

/// <summary>What the app is asked to do from outside it, by the arguments of this run or of a later run
/// that hands them to the app already open. They are all that passes between runs; how a run starts is in
/// <see cref="LaunchConfig"/>. Each option stands alone, so any of them can be given together.</summary>
public sealed record RunArguments
{
    /// <summary>The language to decompile to, or null to keep the one the settings have.</summary>
    public required DecompilationLanguage? Language { get; init; }

    /// <summary>The name of the assembly list to open, made when there is none by that name, or null to open the one
    /// the settings have open.</summary>
    public required string? AssemblyList { get; init; }

    /// <summary>Whether to take every assembly off the list before opening <paramref name="Files"/>.</summary>
    public required bool ClearList { get; init; }

    /// <summary>The files to open and what to show in them.</summary>
    public required FilesToOpen Files { get; init; }

    /// <summary>The arguments that ask for nothing the app can do, as they were given: an option it does not have, a
    /// language it does not have, an option with no value, or a file that is no path.</summary>
    public required List<string> Ignored { get; init; }
}
