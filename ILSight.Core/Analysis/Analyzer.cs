// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Finds the symbols related to an analyzed one in a particular way, its users, its overrides
/// and so on: the analysis of <paramref name="symbol"/>; null when it does not apply to it, or for null.</summary>
public delegate AnalysisRun? Analyzer(ISymbol? symbol);
