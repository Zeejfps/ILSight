using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>What an analysis finds, which names it in the interface.</summary>
public enum AnalysisName
{
    AppliedTo,
    ImplementedBy,
    OverriddenBy,
    ReadBy,
    AssignedBy,
    AddressTakenBy,
    Implements,
    Overrides,
    UsedBy,
    Uses,
    ExposedBy,
    ExtensionMethods,
    InstantiatedBy,
}

/// <summary>One way of relating a symbol to others, such as "Used By", as ILSpy's analyzer
/// offers it: its name and its place among the others.</summary>
public sealed record AnalysisKind
{
    internal AnalysisKind(AnalysisName name, int order, Analyzer analyzer, bool sortsResults = false)
    {
        Name = name;
        Order = order;
        Analyzer = analyzer;
        SortsResults = sortsResults;
    }

    public AnalysisName Name { get; }

    public int Order { get; }

    internal Analyzer Analyzer { get; }

    /// <summary>Whether its results read best sorted by label, so come only once all are found.</summary>
    internal bool SortsResults { get; }
}

/// <summary>A type, member or assembly an analysis is about or found: what a row shows for it and
/// how to go to it.</summary>
public sealed class AnalysisSymbol
{
    internal AnalysisSymbol(ISymbol symbol, SymbolShape shape, NodeAccessibility accessibility, string label, string location, CodeReference.Navigable? reference)
    {
        Symbol = symbol;
        Shape = shape;
        Accessibility = accessibility;
        Label = label;
        Location = location;
        Reference = reference;
    }

    internal ISymbol Symbol { get; }

    /// <summary>What it is: an assembly, or a type or member of some shape.</summary>
    public SymbolShape Shape { get; }

    public NodeAccessibility Accessibility { get; }

    /// <summary>Its name as the tree would show it.</summary>
    public string Label { get; }

    /// <summary>Where it is: the declaring type, or for a type its namespace, and the assembly.</summary>
    public string Location { get; }

    /// <summary>What to navigate to it by; null when it cannot be shown.</summary>
    public CodeReference.Navigable? Reference { get; }

    /// <summary>Its assembly file and metadata token, to find it again with
    /// <see cref="Analyzers.Find"/> in a later run; null for an assembly.</summary>
    public CodeAnchor? Definition
    {
        get
        {
            if (Symbol is not IEntity entity || entity.MetadataToken.IsNil)
                return null;
            var file = entity.ParentModule?.MetadataFile;
            if (file == null)
                return null;
            return new CodeAnchor(CodeAnchorKind.Definition, AssemblyPath.Of(file), MetadataTokens.GetToken(entity.MetadataToken));
        }
    }

    public override string ToString() => Label;
}

/// <summary>
/// ILSpy's analyzer over the open assemblies: the analyses that apply to a symbol, and their
/// results, found by ILSpy's own analyzers.
/// </summary>
public static class Analyzers
{
    private static readonly AnalysisKind[] Kinds =
    [
        new(AnalysisName.AppliedTo, 10, AttributeAppliedToAnalyzer.For),
        new(AnalysisName.ImplementedBy, 10, static s => InheritorsAnalyzer<IEvent>.For(overrides: false, s)),
        new(AnalysisName.OverriddenBy, 20, static s => InheritorsAnalyzer<IEvent>.For(overrides: true, s)),
        new(AnalysisName.ReadBy, 10, static s => FieldAccessAnalyzer.For(FieldAccessKind.Read, s)),
        new(AnalysisName.AssignedBy, 20, static s => FieldAccessAnalyzer.For(FieldAccessKind.Write, s)),
        new(AnalysisName.AddressTakenBy, 30, static s => FieldAccessAnalyzer.For(FieldAccessKind.AddressOf, s)),
        new(AnalysisName.Implements, 40, MemberImplementsInterfaceAnalyzer.For),
        new(AnalysisName.Overrides, 35, MemberOverridesAnalyzer.For),
        new(AnalysisName.ImplementedBy, 40, static s => InheritorsAnalyzer<IMethod>.For(overrides: false, s)),
        new(AnalysisName.OverriddenBy, 30, static s => InheritorsAnalyzer<IMethod>.For(overrides: true, s)),
        new(AnalysisName.UsedBy, 20, MethodUsedByAnalyzer.For),
        new(AnalysisName.Uses, 10, MethodUsesAnalyzer.For),
        new(AnalysisName.UsedBy, 5, MemberUsedByAnalyzer.For),
        new(AnalysisName.ImplementedBy, 10, static s => InheritorsAnalyzer<IProperty>.For(overrides: false, s)),
        new(AnalysisName.OverriddenBy, 20, static s => InheritorsAnalyzer<IProperty>.For(overrides: true, s)),
        new(AnalysisName.ExposedBy, 40, TypeExposedByAnalyzer.For),
        new(AnalysisName.ExtensionMethods, 50, TypeExtensionMethodsAnalyzer.For),
        new(AnalysisName.InstantiatedBy, 20, TypeInstantiatedByAnalyzer.For),
        new(AnalysisName.UsedBy, 30, TypeUsedByAnalyzer.For, sortsResults: true),
    ];

    /// <summary>ILSpy's English name for <paramref name="kind"/>.</summary>
    public static string Header(AnalysisKind kind)
    {
        return kind.Name switch
        {
            AnalysisName.AppliedTo => "Applied To",
            AnalysisName.ImplementedBy => "Implemented By",
            AnalysisName.OverriddenBy => "Overridden By",
            AnalysisName.ReadBy => "Read By",
            AnalysisName.AssignedBy => "Assigned By",
            AnalysisName.AddressTakenBy => "Address Taken By",
            AnalysisName.Implements => "Implements",
            AnalysisName.Overrides => "Overrides",
            AnalysisName.UsedBy => "Used By",
            AnalysisName.Uses => "Uses",
            AnalysisName.ExposedBy => "Exposed By",
            AnalysisName.ExtensionMethods => "Extension Methods",
            AnalysisName.InstantiatedBy => "Instantiated By",
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>The type or member <paramref name="node"/> stands for — for a base or derived type
    /// entry the type it names — or null for any other node.</summary>
    public static AnalysisSymbol? Of(BrowseNode node)
    {
        return node.Subject switch
        {
            NodeSubject.TypeDefinition type => Describe(type.Definition.Symbol),
            NodeSubject.MemberDefinition member => Describe(member.Definition.Symbol),
            NodeSubject.DerivedType derived => Describe(derived.Value.Symbol),
            NodeSubject.BaseType baseType => baseType.Value.Target switch
            {
                BaseTypeTarget.Navigable navigable => Describe(navigable.Definition),
                BaseTypeTarget.Unaddressed unaddressed => Describe(unaddressed.Definition),
                BaseTypeTarget.Unresolved => null,
                _ => throw new UnreachableException(),
            },
            NodeSubject.References or NodeSubject.AssemblyReference or NodeSubject.ReferencedTypes or NodeSubject.TypeReference
                or NodeSubject.MemberReference or NodeSubject.ModuleReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType
                or NodeSubject.BaseTypes or NodeSubject.DerivedTypes or NodeSubject.Resources or NodeSubject.ResourceContainer
                or NodeSubject.ResourceEntry or NodeSubject.Metadata or NodeSubject.MetadataTables or NodeSubject.MetadataTable
                or NodeSubject.Package or NodeSubject.PackageFile or NodeSubject.Module or NodeSubject.Namespace or NodeSubject.FailedAssembly => null,
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>The type or member <paramref name="definition"/> names, loading its assembly's file
    /// without listing it; null when the file or the entity is gone.</summary>
    public static AnalysisSymbol? Find(AssemblyList list, CodeAnchor definition)
    {
        try
        {
            var assembly = list.TryLoadAssembly(definition.FilePath);
            if (assembly == null) return null;
            var entity = assembly.MainModule.ResolveEntity(MetadataTokens.EntityHandle(definition.Token));
            return entity != null ? Describe(entity) : null;
        }
        catch (Exception e) when (e is ArgumentException or BadImageFormatException or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    /// <summary>The analyses that apply to <paramref name="symbol"/>, in ILSpy's order.</summary>
    public static IReadOnlyList<AnalysisKind> KindsFor(AnalysisSymbol symbol) =>
        Kinds.Where(k => k.Analyzer(symbol.Symbol) != null).OrderBy(static k => k.Order).ToArray();

    /// <summary>The accessors of a property or event, which ILSpy analyzes on their own.</summary>
    public static IReadOnlyList<AnalysisSymbol> AccessorsOf(AnalysisSymbol symbol) =>
        symbol.Symbol is IEntity entity ? Members.Accessors(entity).Select(static m => Describe(m)!).ToArray() : [];

    /// <summary>Runs <paramref name="kind"/> on <paramref name="symbol"/> over the open assemblies on
    /// the thread pool, yielding results in batches as they are found. Results come sorted when the
    /// analysis asks for it, after all are found.</summary>
    public static IAsyncEnumerable<IReadOnlyList<AnalysisSymbol>> RunAsync(AssemblyList list, AnalysisSymbol symbol, AnalysisKind kind,
        CancellationToken cancellationToken = default) =>
        Batches.Stream<AnalysisSymbol>((emit, token) =>
        {
            var run = kind.Analyzer(symbol.Symbol);
            if (run == null)
                throw new ArgumentException($"The analysis {kind.Name} does not apply to {symbol.Label}.", nameof(kind));
            var found = run(new AnalyzerContext { AssemblyList = list, CancellationToken = token })
                .Select(Describe).OfType<AnalysisSymbol>();
            if (kind.SortsResults)
                found = found.OrderBy(static s => s.Label, StringComparer.OrdinalIgnoreCase);
            foreach (var result in found)
            {
                token.ThrowIfCancellationRequested();
                emit(result);
            }
        }, cancellationToken);

    private static AnalysisSymbol? Describe(ISymbol symbol)
    {
        switch (symbol)
        {
            case IEntity entity:
                return new AnalysisSymbol(entity, EntityText.SymbolShapeOf(entity), EntityText.Map(entity.Accessibility), EntityText.Label(entity),
                    Where(entity), CodeReference.To(entity));
            case IModule module:
                return new AnalysisSymbol(module, new SymbolShape.Assembly(), NodeAccessibility.Public, module.AssemblyName, module.MetadataFile?.FileName ?? "",
                    module.MetadataFile != null ? CodeReference.ToAssembly(module.AssemblyName, AssemblyPath.Of(module.MetadataFile)) : null);
            default:
                return null;
        }
    }

    private static string Where(IEntity entity)
    {
        var outer = entity.DeclaringTypeDefinition;
        var container = outer != null ? EntityText.FullName(outer) : entity is ITypeDefinition type ? type.Namespace : "";
        var assembly = entity.ParentModule?.AssemblyName;
        if (assembly == null)
            return container;
        return container.Length == 0 ? assembly : $"{container} ({assembly})";
    }
}
