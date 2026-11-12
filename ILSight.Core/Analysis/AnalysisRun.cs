// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>An analysis of one symbol, run against the open assemblies.</summary>
public delegate IEnumerable<ISymbol> AnalysisRun(AnalyzerContext context);
