using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.Metadata;
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

    /// <summary>The analyses that apply to the symbol, in the order they are offered, worked out when it was read.</summary>
    public required IReadOnlyList<AnalysisKind> Kinds { get; init; }

    /// <summary>The accessors of a property or event, which are analyzed on their own; none for anything else.</summary>
    public required IReadOnlyList<AnalysisSymbol> Accessors { get; init; }

    /// <summary>Whether it is in <paramref name="item"/>, an assembly or a package on the list, or read out of
    /// one; or is that assembly.</summary>
    public bool IsIn(LoadedAssembly item)
    {
        var file = FindFile();
        if (file is null)
        {
            return false;
        }

        var path = AssemblyPath.Of(file.FileName);
        if (path.Equals(item.FilePath))
        {
            return true;
        }

        return item is LoadedPackage && path.IsUnder(item.FilePath);
    }

    /// <summary>The file of the assembly it is in, or is; null when it has none.</summary>
    private MetadataFile? FindFile()
    {
        if (Symbol is IModule module)
        {
            return module.MetadataFile;
        }

        if (Symbol is IEntity entity)
        {
            return entity.ParentModule?.MetadataFile;
        }

        return null;
    }

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
