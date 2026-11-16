// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Collections.Concurrent;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.AnalysisArea;

/// <summary>What an analysis runs against: the open assemblies, the type systems of the modules it can reach, read
/// before it began, and cancellation.</summary>
public sealed class AnalyzerContext
{
    private (MetadataFile File, EntityHandle Handle, CodeMappingInfo Mapping)? _mapping;

    public required AssemblyList AssemblyList { get; init; }

    /// <summary>The reads of the type systems of the modules the analysis can reach, each awaited before it began
    /// (<see cref="AnalyzerScope.ReadReachableAsync"/>).</summary>
    public required ModuleTypeSystems TypeSystems { get; init; }

    /// <summary>The file of the module the analyzed symbol is in, where a part of the symbol the library fails reading is
    /// noted as passed over.</summary>
    public required MetadataFile AnalyzedFile { get; init; }

    /// <summary>The modules the analysis reached that the library could not read, to name beside its results.</summary>
    internal ConcurrentDictionary<LoadedModule, UnreadableModule> Unreadable { get; } = new();

    /// <summary>The parts of the modules the analysis read that the library couldn't read, to name beside its
    /// results.</summary>
    internal PassedOver PassedOver { get; } = new();

    public CancellationToken CancellationToken { get; internal init; }

    /// <summary>The mapping of the compiler-generated code around the entity <paramref name="handle"/> names in
    /// <paramref name="file"/>, when it is the one kept (<see cref="KeepMapping"/>); null when another is.</summary>
    internal CodeMappingInfo? FindMapping(MetadataFile file, EntityHandle handle)
    {
        var mapping = _mapping;
        if (mapping is null || mapping.Value.File != file || mapping.Value.Handle != handle)
        {
            return null;
        }

        return mapping.Value.Mapping;
    }

    /// <summary>Keeps <paramref name="mapping"/>, read for the entity <paramref name="handle"/> names in
    /// <paramref name="file"/>, as the last one asked for, in place of the one kept before.</summary>
    internal void KeepMapping(MetadataFile file, EntityHandle handle, CodeMappingInfo mapping)
    {
        _mapping = (file, handle, mapping);
    }
}
