using ICSharpCode.Decompiler;

namespace ILSight.Core;

/// <summary>One of the decompiler's on/off choices, named as the decompiler names it; <see cref="All"/>
/// holds every one there is.</summary>
public sealed class DecompilerToggle
{
    public required string Name { get; init; }

    public required DecompilerToggleGroup Group { get; init; }

    public required bool DefaultValue { get; init; }

    /// <summary>Every toggle that changes decompiled code, in the decompiler's order.</summary>
    public static IReadOnlyList<DecompilerToggle> All { get; } = DecompilerToggles.Discover();

    internal static readonly Dictionary<string, DecompilerToggle> ByName = All.ToDictionary(static t => t.Name);

    /// <summary>Whether locals take their names from the assembly's PDB; the one toggle IL reads.</summary>
    public static DecompilerToggle UseDebugSymbols => ByName[nameof(DecompilerSettings.UseDebugSymbols)];

    /// <summary>Whether resolving a reference reads its assembly from a file, rather than only finding
    /// one already listed; applies as assemblies are read.</summary>
    public static DecompilerToggle AutoLoadAssemblyReferences => ByName[nameof(DecompilerSettings.AutoLoadAssemblyReferences)];

    /// <summary>Whether Windows Runtime metadata reads as the .NET types it projects to; applies as
    /// assemblies are read.</summary>
    public static DecompilerToggle ApplyWindowsRuntimeProjections => ByName[nameof(DecompilerSettings.ApplyWindowsRuntimeProjections)];
}
