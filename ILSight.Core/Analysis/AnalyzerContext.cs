// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Collections.Concurrent;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>What an analysis runs against: the open assemblies, the type systems of the modules it can reach, read
/// before it began, and cancellation.</summary>
public sealed class AnalyzerContext
{
    private (MetadataFile File, EntityHandle Handle, CodeMappingInfo Mapping)? _mapping;

    public required AssemblyList AssemblyList { get; init; }

    /// <summary>The reads of the type systems of the modules the analysis can reach, each awaited before it began
    /// (<see cref="AnalyzerScope.ReadReachableAsync"/>).</summary>
    public required ModuleTypeSystems TypeSystems { get; init; }

    /// <summary>The modules the analysis reached that the library could not read, to name beside its results.</summary>
    internal ConcurrentDictionary<LoadedModule, UnreadableModule> Unreadable { get; } = new();

    public CancellationToken CancellationToken { get; internal init; }

    /// <summary>How C# maps the compiler-generated code around <paramref name="entity"/> to the
    /// methods it came from; the last one asked for is kept.</summary>
    internal CodeMappingInfo EnsureMapping(IEntity entity)
    {
        var file = DecompilerFacts.GetMetadataFile(entity);
        var mapping = _mapping;
        if (mapping is null || mapping.Value.File != file || mapping.Value.Handle != entity.MetadataToken)
        {
            mapping = (file, entity.MetadataToken, CSharpDecompiler.GetCodeMappingInfo(file, entity.MetadataToken));
            _mapping = mapping;
        }
        return mapping.Value.Mapping;
    }
}
