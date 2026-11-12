namespace ILSight.Core.Analysis;

/// <summary>A type, member or assembly an analysis is about or found: what a row shows for it and
/// how to go to it. A handle: it holds the values read when it was described, and none of the library's objects,
/// which <see cref="Analyzers"/> keeps against it for running analyses on it.</summary>
public sealed class AnalysisSymbol
{
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

    /// <summary>The file of the assembly it is in, or is; null when it is in none.</summary>
    public required AssemblyPath? File { get; init; }

    /// <summary>Its assembly file and metadata token, to find it again with
    /// <see cref="Analyzers.Find"/> in a later run; null for an assembly.</summary>
    public required CodeAnchor? Definition { get; init; }

    /// <summary>Whether it is in <paramref name="item"/>, an assembly or a package on the list, or read out of
    /// one; or is that assembly.</summary>
    public bool IsIn(LoadedAssembly item)
    {
        var file = File;
        if (file is null)
        {
            return false;
        }

        if (file.Equals(item.FilePath))
        {
            return true;
        }

        return item is LoadedPackage && file.IsUnder(item.FilePath);
    }

    /// <summary>Whether it is in the file at <paramref name="path"/>, an assembly or a package, or read out of it; or
    /// is that assembly.</summary>
    public bool IsIn(AssemblyPath path)
    {
        var file = File;
        if (file is null)
        {
            return false;
        }

        return file.Equals(path) || file.IsUnder(path);
    }

    public override string ToString()
    {
        return Label;
    }
}
