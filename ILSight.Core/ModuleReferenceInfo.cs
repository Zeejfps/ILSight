using System.Reflection.Metadata;

namespace ILSight.Core;

/// <summary>A module <see cref="Module"/> references: a native library it calls into, or another module
/// of the assembly, which holds metadata when the assembly's file table says so.</summary>
public sealed class ModuleReferenceInfo
{
    public required LoadedModule Module { get; init; }

    public required string Name { get; init; }

    public required bool ContainsMetadata { get; init; }

    /// <summary>The methods the module imports from it.</summary>
    public required IReadOnlyList<MethodDefinitionHandle> Imports { get; init; }
}
