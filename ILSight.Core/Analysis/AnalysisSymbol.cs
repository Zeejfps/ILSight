using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>A type, member or assembly an analysis is about or found: what a row shows for it and
/// how to go to it.</summary>
public sealed class AnalysisSymbol
{
    public required ISymbol Symbol { get; init; }

    /// <summary>What it is: an assembly, or a type or member of some shape.</summary>
    public required SymbolShape Shape { get; init; }

    public required NodeAccessibility Accessibility { get; init; }

    /// <summary>Its name as the tree would show it.</summary>
    public required string Label { get; init; }

    /// <summary>Where it is: the declaring type, or for a type its namespace, and the assembly.</summary>
    public required string Location { get; init; }

    /// <summary>What to navigate to it by; null when it cannot be shown.</summary>
    public required CodeReference.Navigable? Reference { get; init; }

    /// <summary>Its assembly file and metadata token, to find it again with
    /// <see cref="Analyzers.Find"/> in a later run; null for an assembly.</summary>
    public CodeAnchor? Definition
    {
        get
        {
            if (Symbol is not IEntity entity || entity.MetadataToken.IsNil)
            {
                return null;
            }

            var file = entity.ParentModule?.MetadataFile;
            if (file is null)
            {
                return null;
            }

            return new CodeAnchor(CodeAnchorKind.Definition, AssemblyPath.Of(file.FileName), MetadataTokens.GetToken(entity.MetadataToken));
        }
    }

    public override string ToString()
    {
        return Label;
    }
}
