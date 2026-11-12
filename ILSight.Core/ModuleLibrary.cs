using System.Collections.Concurrent;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>The library's objects for a module that was read: the file, the resolver of what it references, and what
/// takes reading, held as <see cref="Lazy{T}"/>s, read once whichever thread asks first. Kept by
/// <see cref="LoadedAssemblies"/> against the <see cref="LoadedModule"/> made with them, so they live as long as the
/// module does, and read only by the I/O side.</summary>
internal sealed class ModuleLibrary
{
    public required MetadataFile File { get; init; }

    public required AssemblyResolver Resolver { get; init; }

    public required Lazy<DecompilerTypeSystem> TypeSystem { get; init; }

    /// <summary>The assembly's portable PDB; null when it has none.</summary>
    public required Lazy<DebugSymbols?> DebugSymbols { get; init; }

    /// <summary>What the assembly uses of the assemblies it references.</summary>
    public required Lazy<ReferenceUsage> ReferenceUsage { get; init; }

    /// <summary>The type systems of the settings that turn language features on or off that
    /// <see cref="TypeSystem"/> models.</summary>
    public ConcurrentDictionary<TypeSystemOptions, Lazy<DecompilerTypeSystem>> TypeSystems { get; } = new();
}
