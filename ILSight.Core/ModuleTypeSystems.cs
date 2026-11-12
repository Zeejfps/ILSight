using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>The type systems of the modules a unit reads, each read awaited and matched once
/// (<see cref="LoadedAssemblies.ReadTypeSystemsAsync"/>): those read, by the file each was read from, and those the
/// library could not read, each with why, for the unit to name beside what the others gave.</summary>
public sealed class ModuleTypeSystems
{
    public required IReadOnlyDictionary<MetadataFile, DecompilerTypeSystem> Read { get; init; }

    public required IReadOnlyList<UnreadableModule> Unreadable { get; init; }
}
