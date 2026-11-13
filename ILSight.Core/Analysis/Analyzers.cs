using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>
/// ILSpy's analyzer over the open assemblies: the analyses that apply to a symbol, and their
/// results, found by ILSpy's own analyzers.
/// </summary>
public static class Analyzers
{
    // Weak, keyed by the AnalysisSymbol handle, so the library's symbol lives as long as the handle does.
    private static readonly ConditionalWeakTable<AnalysisSymbol, ISymbol> Symbols = new();

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

    /// <summary>The type or member a node of <paramref name="subject"/> stands for — for a base or derived type
    /// entry the type it names — or null for any other node.</summary>
    internal static AnalysisSymbol? Of(NodeSubject subject)
    {
        return subject.Match<AnalysisSymbol?>(
            module: static _ => null,
            @namespace: static _ => null,
            failedAssembly: static _ => null,
            typeDefinition: static type => Describe(NodeDefinitions.GetSymbol(type.Definition)),
            memberDefinition: static member => Describe(NodeDefinitions.GetSymbol(member.Definition)),
            references: static _ => null,
            assemblyReference: static _ => null,
            referencedTypes: static _ => null,
            typeReference: static _ => null,
            memberReference: static _ => null,
            moduleReference: static _ => null,
            importedMethod: static _ => null,
            forwardedType: static _ => null,
            baseTypes: static _ => null,
            baseType: static baseType => DescribeBase(baseType.Value),
            derivedTypes: static _ => null,
            derivedType: static derived => Describe(NodeDefinitions.GetSymbol(derived.Value)),
            unreadableModule: static _ => null,
            resources: static _ => null,
            resourceContainer: static _ => null,
            resourceEntry: static _ => null,
            metadata: static _ => null,
            metadataTables: static _ => null,
            metadataTable: static _ => null,
            package: static _ => null,
            packageFile: static _ => null);
    }

    private static AnalysisSymbol? DescribeBase(BaseTypeInfo baseType)
    {
        var definition = NodeDefinitions.GetSymbols(baseType).Definition;
        if (definition is null)
        {
            return null;
        }

        return Describe(definition);
    }

    /// <summary>The type or member <paramref name="definition"/> names, loading its assembly's file
    /// without listing it, once the read of its type system ended; not found when the file or the entity is gone,
    /// unreadable when the library could not read the file.</summary>
    public static async Task<Lookup<AnalysisSymbol>> FindAsync(AssemblyList list, CodeAnchor definition, CancellationToken ct)
    {
        var assembly = TryLoad(list, definition);
        if (assembly is null)
        {
            return new Lookup<AnalysisSymbol>.NotFound();
        }

        using var reading = LoadedAssemblies.BeginReading();
        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, assembly, ct);
        return read.Match(
            r => Lookup.FromNullable(Find(r.TypeSystem.MainModule, definition)),
            u => Lookup.FromUnreadable<AnalysisSymbol>(assembly, u),
            static _ => new Lookup<AnalysisSymbol>.NotFound());
    }

    private static LoadedModule? TryLoad(AssemblyList list, CodeAnchor definition)
    {
        try
        {
            return AssemblyListing.TryLoadAssembly(list, definition.FilePath);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static AnalysisSymbol? Find(MetadataModule main, CodeAnchor definition)
    {
        try
        {
            var entity = main.ResolveEntity(MetadataTokens.EntityHandle(definition.Token));
            return entity is not null ? Describe(entity) : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static IReadOnlyList<AnalysisKind> ListKinds(ISymbol symbol)
    {
        return Kinds.Where(k => k.Prepare(symbol) is not null).OrderBy(static k => k.Order).ToArray();
    }

    /// <summary>Runs <paramref name="kind"/> on <paramref name="symbol"/> over the open assemblies on
    /// the thread pool, once the reads of the type systems of the modules it can reach ended, yielding results in
    /// batches as they are found, and after them each module it reached that the library could not read. Results come
    /// sorted when the analysis asks for it, after all are found. A unit reading the symbol's module and those it
    /// scans: a module released (closed) before is passed over, the symbol's own finding nothing.</summary>
    public static IAsyncEnumerable<IReadOnlyList<AnalysisFind>> RunAsync(AssemblyList list, AnalysisSymbol symbol, AnalysisKind kind,
        CancellationToken cancellationToken = default)
    {
        return Batches.StreamAsync<AnalysisFind>(async (emit, token) =>
        {
            var entity = GetSymbol(symbol);
            var own = OwnModuleOf(list, entity);
            using var reading = LoadedAssemblies.BeginReading();
            if (!LoadedAssemblies.TryRead(reading, own))
            {
                return;
            }

            var run = kind.Prepare(entity);
            if (run is null)
            {
                throw new ArgumentException($"The analysis {kind.Name} does not apply to {symbol.Label}.", nameof(kind));
            }

            var reads = await AnalyzerScope.ReadReachableAsync(reading, list, own, token);
            var context = new AnalyzerContext { AssemblyList = list, TypeSystems = reads, CancellationToken = token };
            var found = run(context).Select(Describe).OfType<AnalysisSymbol>();
            if (kind.SortsResults)
            {
                found = found.OrderBy(static s => s.Label, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var result in found)
            {
                token.ThrowIfCancellationRequested();
                emit(new AnalysisFind.Found { Symbol = result });
            }

            foreach (var unreadable in context.Unreadable.Values.OrderBy(static u => u.Module.FilePath.FileName, StringComparer.OrdinalIgnoreCase))
            {
                emit(new AnalysisFind.Unreadable { Name = unreadable.Module.FilePath.FileName, Why = unreadable.Why.Message });
            }
        }, cancellationToken);
    }

    private static LoadedModule OwnModuleOf(AssemblyList list, ISymbol symbol)
    {
        var file = symbol switch
        {
            IModule module => module.MetadataFile,
            IEntity entity => entity.ParentModule?.MetadataFile,
            _ => null,
        };
        if (file is null)
        {
            throw new InvalidOperationException($"{symbol.Name} is in no module to analyze.");
        }

        return AssemblyListing.GetLoadedFrom(list, file);
    }

    private static AnalysisSymbol? Describe(ISymbol symbol)
    {
        switch (symbol)
        {
            case IEntity entity:
                return DescribeEntity(entity);
            case IModule module:
                return Keep(DescribeModule(module), module);
            default:
                return null;
        }
    }

    private static AnalysisSymbol DescribeModule(IModule module)
    {
        var file = module.MetadataFile is null ? null : AssemblyPath.Of(module.MetadataFile.FileName);
        return new AnalysisSymbol
        {
            Shape = new SymbolShape.Assembly(), Accessibility = NodeAccessibility.Public, Label = module.AssemblyName,
            Location = module.MetadataFile?.FileName ?? "",
            Reference = file is not null ? CodeReferences.CreateAssembly(module.AssemblyName, file) : null,
            Kinds = ListKinds(module), Accessors = [], File = file, Definition = null,
        };
    }

    private static AnalysisSymbol DescribeEntity(IEntity entity)
    {
        var file = entity.ParentModule?.MetadataFile;
        var path = file is null ? null : AssemblyPath.Of(file.FileName);
        var described = new AnalysisSymbol
        {
            Shape = EntityText.SymbolShapeOf(entity), Accessibility = EntityText.Map(entity.Accessibility), Label = EntityText.Label(entity),
            Location = Where(entity), Reference = CodeReferences.Find(entity), Kinds = ListKinds(entity),
            Accessors = Members.Accessors(entity).Select(static m => DescribeEntity(m)).ToArray(),
            File = path, Definition = DefinitionOf(entity, path),
        };
        return Keep(described, entity);
    }

    private static CodeAnchor? DefinitionOf(IEntity entity, AssemblyPath? path)
    {
        if (path is null || entity.MetadataToken.IsNil)
        {
            return null;
        }

        return new CodeAnchor
        {
            Kind = CodeAnchorKind.Definition,
            FilePath = path,
            Token = MetadataTokens.GetToken(entity.MetadataToken),
        };
    }

    private static AnalysisSymbol Keep(AnalysisSymbol described, ISymbol symbol)
    {
        Symbols.Add(described, symbol);
        return described;
    }

    private static ISymbol GetSymbol(AnalysisSymbol symbol)
    {
        if (!Symbols.TryGetValue(symbol, out var kept))
        {
            throw new InvalidOperationException($"'{symbol.Label}' was not described by {nameof(Analyzers)}.");
        }

        return kept;
    }

    private static string Where(IEntity entity)
    {
        var outer = entity.DeclaringTypeDefinition;
        var container = outer is not null ? EntityText.FullName(outer) : entity is ITypeDefinition type ? type.Namespace : "";
        var assembly = entity.ParentModule?.AssemblyName;
        if (assembly is null)
        {
            return container;
        }

        return container.Length == 0 ? assembly : $"{container} ({assembly})";
    }
}
