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
public sealed class AnalysisKind
{
    public required AnalysisName Name { get; init; }

    public required int Order { get; init; }

    public required Analyzer Prepare { get; init; }

    /// <summary>Whether its results read best sorted by label, so come only once all are found.</summary>
    internal bool SortsResults { get; init; }
}

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
                return null;
            var file = entity.ParentModule?.MetadataFile;
            if (file == null)
                return null;
            return new CodeAnchor(CodeAnchorKind.Definition, AssemblyPath.Of(file.FileName), MetadataTokens.GetToken(entity.MetadataToken));
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
        new AnalysisKind { Name = AnalysisName.AppliedTo, Order = 10, Prepare = AttributeAppliedToAnalyzer.Prepare },
        new AnalysisKind { Name = AnalysisName.ImplementedBy, Order = 10, Prepare = static s => InheritorsAnalyzer<IEvent>.Prepare(overrides: false, s) },
        new AnalysisKind { Name = AnalysisName.OverriddenBy, Order = 20, Prepare = static s => InheritorsAnalyzer<IEvent>.Prepare(overrides: true, s) },
        new AnalysisKind { Name = AnalysisName.ReadBy, Order = 10, Prepare = static s => FieldAccessAnalyzer.Prepare(FieldAccessKind.Read, s) },
        new AnalysisKind { Name = AnalysisName.AssignedBy, Order = 20, Prepare = static s => FieldAccessAnalyzer.Prepare(FieldAccessKind.Write, s) },
        new AnalysisKind { Name = AnalysisName.AddressTakenBy, Order = 30, Prepare = static s => FieldAccessAnalyzer.Prepare(FieldAccessKind.AddressOf, s) },
        new AnalysisKind { Name = AnalysisName.Implements, Order = 40, Prepare = MemberImplementsInterfaceAnalyzer.Prepare },
        new AnalysisKind { Name = AnalysisName.Overrides, Order = 35, Prepare = MemberOverridesAnalyzer.Prepare },
        new AnalysisKind { Name = AnalysisName.ImplementedBy, Order = 40, Prepare = static s => InheritorsAnalyzer<IMethod>.Prepare(overrides: false, s) },
        new AnalysisKind { Name = AnalysisName.OverriddenBy, Order = 30, Prepare = static s => InheritorsAnalyzer<IMethod>.Prepare(overrides: true, s) },
        new AnalysisKind { Name = AnalysisName.UsedBy, Order = 20, Prepare = MethodUsedByAnalyzer.Prepare },
        new AnalysisKind { Name = AnalysisName.Uses, Order = 10, Prepare = MethodUsesAnalyzer.Prepare },
        new AnalysisKind { Name = AnalysisName.UsedBy, Order = 5, Prepare = MemberUsedByAnalyzer.Prepare },
        new AnalysisKind { Name = AnalysisName.ImplementedBy, Order = 10, Prepare = static s => InheritorsAnalyzer<IProperty>.Prepare(overrides: false, s) },
        new AnalysisKind { Name = AnalysisName.OverriddenBy, Order = 20, Prepare = static s => InheritorsAnalyzer<IProperty>.Prepare(overrides: true, s) },
        new AnalysisKind { Name = AnalysisName.ExposedBy, Order = 40, Prepare = TypeExposedByAnalyzer.Prepare },
        new AnalysisKind { Name = AnalysisName.ExtensionMethods, Order = 50, Prepare = TypeExtensionMethodsAnalyzer.Prepare },
        new AnalysisKind { Name = AnalysisName.InstantiatedBy, Order = 20, Prepare = TypeInstantiatedByAnalyzer.Prepare },
        new AnalysisKind { Name = AnalysisName.UsedBy, Order = 30, Prepare = TypeUsedByAnalyzer.Prepare, SortsResults = true },
    ];

    /// <summary>ILSpy's English name for <paramref name="kind"/>.</summary>
    public static string GetHeader(AnalysisKind kind)
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
        Kinds.Where(k => k.Prepare(symbol.Symbol) != null).OrderBy(static k => k.Order).ToArray();

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
            var run = kind.Prepare(symbol.Symbol);
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
                return new AnalysisSymbol { Symbol = entity, Shape = EntityText.SymbolShapeOf(entity), Accessibility = EntityText.Map(entity.Accessibility), Label = EntityText.Label(entity), Location = Where(entity), Reference = CodeReferences.Find(entity) };
            case IModule module:
                return new AnalysisSymbol { Symbol = module, Shape = new SymbolShape.Assembly(), Accessibility = NodeAccessibility.Public, Label = module.AssemblyName, Location = module.MetadataFile?.FileName ?? "", Reference = module.MetadataFile != null ? CodeReferences.CreateAssembly(module.AssemblyName, AssemblyPath.Of(module.MetadataFile.FileName)) : null };
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
