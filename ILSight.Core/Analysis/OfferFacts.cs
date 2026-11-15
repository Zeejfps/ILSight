using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>What the app's table of analyses (<see cref="AnalysisKind.Applies"/>) decides on, read out of a symbol with
/// the rest of its description, inside the library's catch: its sort and kind, its flags, the kinds of the type
/// declaring it, whether it has accessors and whether a base type is <see cref="System.Attribute"/>. A flag that doesn't
/// apply to the symbol's sort is false.</summary>
public sealed record OfferFacts
{
    public required AnalyzedSort Sort { get; init; }

    /// <summary>The library's kind of the symbol (a method, an operator, an indexer, ...).</summary>
    public required SymbolKind SymbolKind { get; init; }

    public required bool IsStatic { get; init; }

    public required bool IsAbstract { get; init; }

    public required bool IsOverride { get; init; }

    public required bool IsOverridable { get; init; }

    public required bool IsVirtual { get; init; }

    public required bool IsConst { get; init; }

    public required bool HasBody { get; init; }

    public required bool HasParentModule { get; init; }

    /// <summary>The kind of the member's declaring type as the member names it; null for a symbol that is no member.</summary>
    public required TypeKind? DeclaringTypeKind { get; init; }

    /// <summary>The kind of the definition of the type declaring the symbol; null when none does.</summary>
    public required TypeKind? DeclaringDefinitionKind { get; init; }

    /// <summary>Whether a property or event has an accessor.</summary>
    public required bool HasAccessors { get; init; }

    /// <summary>Whether a type has <see cref="System.Attribute"/> among its base types, by the library's walk.</summary>
    public required bool IsAttribute { get; init; }

    /// <summary>The facts of a module, to which no analysis applies.</summary>
    public static OfferFacts OfModule { get; } = new()
    {
        Sort = AnalyzedSort.Module, SymbolKind = SymbolKind.Module, IsStatic = false, IsAbstract = false, IsOverride = false,
        IsOverridable = false, IsVirtual = false, IsConst = false, HasBody = false, HasParentModule = false, DeclaringTypeKind = null,
        DeclaringDefinitionKind = null, HasAccessors = false, IsAttribute = false,
    };
}
