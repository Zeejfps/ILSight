// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Analysis;

/// <summary>What an analysis runs against: the open assemblies and cancellation.</summary>
public sealed class AnalyzerContext
{
    public (MetadataFile File, EntityHandle Handle, CodeMappingInfo Mapping)? Mapping;

    public required AssemblyList AssemblyList { get; init; }

    public CancellationToken CancellationToken { get; internal init; }
}
