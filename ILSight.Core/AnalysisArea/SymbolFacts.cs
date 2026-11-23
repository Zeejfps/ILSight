using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>What describing a symbol reads of it (<see cref="Analyzers"/>), inside the library's catch. The container is
/// the full name of the type declaring it, else a type's namespace, else empty (a module's: its file); a module has no
/// assembly name of its own here.</summary>
internal sealed record SymbolFacts
{
    public required ISymbol Symbol { get; init; }
    public required SymbolShape Shape { get; init; }
    public required NodeAccessibility Accessibility { get; init; }
    public required string Label { get; init; }
    public required string Container { get; init; }
    public required string? AssemblyName { get; init; }
    public required Navigable? Reference { get; init; }
    public required OfferFacts Offers { get; init; }
    public required IReadOnlyList<SymbolFacts> Accessors { get; init; }
    public required AssemblyPath? File { get; init; }
    public required CodeAnchor? Definition { get; init; }
}
