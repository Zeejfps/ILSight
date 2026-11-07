// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Finds the symbols related to an analyzed one in a particular way, its users, its overrides
/// and so on: the analysis of <paramref name="symbol"/>; null when it does not apply to it, or for null.</summary>
public delegate AnalysisRun? Analyzer(ISymbol? symbol);

/// <summary>An analysis of one symbol, run against the open assemblies.</summary>
public delegate IEnumerable<ISymbol> AnalysisRun(AnalyzerContext context);

/// <summary>What an analysis runs against: the open assemblies and cancellation.</summary>
public sealed class AnalyzerContext
{
    public (MetadataFile File, EntityHandle Handle, CodeMappingInfo Mapping)? Mapping;

    public required AssemblyList AssemblyList { get; init; }

    public CancellationToken CancellationToken { get; internal init; }
}
