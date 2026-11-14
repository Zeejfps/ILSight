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
    /// without listing it, once the read of its type system ended; not found when the file is gone or its module has
    /// no such row, unreadable when the library could not read the module; a file there that can't be read, and what
    /// else the library throws resolving or describing it, <see cref="Outcome{T}.Failed"/>.</summary>
    public static async Task<Outcome<Lookup<AnalysisSymbol>>> FindAsync(AssemblyList list, CodeAnchor definition, CancellationToken ct)
    {
        LoadedModule? assembly;
        try
        {
            assembly = AssemblyListing.FindAssemblyToLookIn(list, definition.FilePath);
        }
        catch (AssemblyLoadException e)
        {
            return new Outcome<Lookup<AnalysisSymbol>>.Failed(e);
        }

        if (assembly is null)
        {
            return new Outcome<Lookup<AnalysisSymbol>>.Ok(new Lookup<AnalysisSymbol>.NotFound());
        }

        using var reading = LoadedAssemblies.BeginReading();
        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, assembly, ct);
        return read.Match(
            r => Find(assembly, r.TypeSystem.MainModule, definition, ct),
            u => new Outcome<Lookup<AnalysisSymbol>>.Ok(Lookup.FromUnreadable<AnalysisSymbol>(assembly, u)),
            static _ => new Outcome<Lookup<AnalysisSymbol>>.Ok(new Lookup<AnalysisSymbol>.NotFound()));
    }

    // Whether the saved token still names a row is asked of the module first; what the library throws resolving one
    // that does is a failure: the module's own as unreadable, the rest with why.
    private static Outcome<Lookup<AnalysisSymbol>> Find(LoadedModule assembly, MetadataModule main, CodeAnchor definition, CancellationToken ct)
    {
        SymbolFacts? facts;
        try
        {
            if (!SavedTokens.IsInModule(main, definition.Token))
            {
                return new Outcome<Lookup<AnalysisSymbol>>.Ok(new Lookup<AnalysisSymbol>.NotFound());
            }

            var entity = main.ResolveEntity(MetadataTokens.EntityHandle(definition.Token));
            facts = entity is not null ? ReadFacts(entity) : null;
        }
        catch (BadImageFormatException e)
        {
            return new Outcome<Lookup<AnalysisSymbol>>.Ok(new Lookup<AnalysisSymbol>.Unreadable { Module = new UnreadableModule { Module = assembly, Why = e } });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<Lookup<AnalysisSymbol>>.Failed(e);
        }

        return new Outcome<Lookup<AnalysisSymbol>>.Ok(Lookup.FromNullable(facts is not null ? Compose(facts) : null));
    }

    private static IReadOnlyList<AnalysisKind> ListKinds(ISymbol symbol)
    {
        return Kinds.Where(k => k.Prepare(symbol) is not null).OrderBy(static k => k.Order).ToArray();
    }

    /// <summary>Runs <paramref name="kind"/> on <paramref name="symbol"/> over the open assemblies on
    /// the thread pool, once the reads of the type systems of the modules it can reach ended, yielding results in
    /// batches as they are found, and after them each module it reached that the library could not read. Results come
    /// sorted when the analysis asks for it, after all are found. A unit reading the symbol's module and those it
    /// scans: a module released (closed) before is passed over, the symbol's own finding nothing. What the library
    /// throws as the analysis reads ends the stream with <see cref="Outcome{T}.Failed"/>, after what was found before
    /// it; the cancellation of <paramref name="cancellationToken"/> is let through.</summary>
    public static IAsyncEnumerable<Outcome<IReadOnlyList<AnalysisFind>>> RunAsync(AssemblyList list, AnalysisSymbol symbol, AnalysisKind kind,
        CancellationToken cancellationToken = default)
    {
        return Batches.StreamOutcomesAsync<AnalysisFind>(async (emit, token) =>
        {
            var entity = GetSymbol(symbol);
            var own = OwnModuleOf(list, entity);
            using var reading = LoadedAssemblies.BeginReading();
            if (!LoadedAssemblies.TryRead(reading, own))
            {
                return new Outcome.Done();
            }

            var run = kind.Prepare(entity);
            if (run is null)
            {
                throw new ArgumentException($"The analysis {kind.Name} does not apply to {symbol.Label}.", nameof(kind));
            }

            var reads = await AnalyzerScope.ReadReachableAsync(reading, list, own, token);
            var context = new AnalyzerContext { AssemblyList = list, TypeSystems = reads, CancellationToken = token };
            var found = EmitFound(run, context, kind.SortsResults, emit, token);
            if (found is Outcome.Failed)
            {
                return found;
            }

            foreach (var unreadable in context.Unreadable.Values.OrderBy(static u => u.Module.FilePath.FileName, StringComparer.OrdinalIgnoreCase))
            {
                emit(new AnalysisFind.Unreadable { Name = unreadable.Module.FilePath.FileName, Why = unreadable.Why.Message });
            }

            return new Outcome.Done();
        }, cancellationToken);
    }

    private static Outcome EmitFound(AnalysisRun run, AnalyzerContext context, bool sorts, Action<AnalysisFind> emit, CancellationToken ct)
    {
        if (!sorts)
        {
            return ReadEach(run, context, s => emit(new AnalysisFind.Found { Symbol = s }), ct);
        }

        var all = new List<AnalysisSymbol>();
        var read = ReadEach(run, context, all.Add, ct);
        if (read is Outcome.Failed)
        {
            return read;
        }

        foreach (var result in all.OrderBy(static s => s.Label, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            emit(new AnalysisFind.Found { Symbol = result });
        }

        return read;
    }

    // The analysis reads the library's symbols as it is enumerated, and reading each one's facts reads it again; the
    // description is composed from the facts outside the catch, so that a bug of the app's is not the library's.
    private static Outcome ReadEach(AnalysisRun run, AnalyzerContext context, Action<AnalysisSymbol> take, CancellationToken ct)
    {
        IEnumerator<ISymbol>? each = null;
        try
        {
            while (true)
            {
                SymbolFacts? facts;
                try
                {
                    each ??= run(context).GetEnumerator();
                    if (!each.MoveNext())
                    {
                        return new Outcome.Done();
                    }

                    facts = ReadFacts(each.Current);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    return new Outcome.Failed(e);
                }

                ct.ThrowIfCancellationRequested();
                if (facts is not null)
                {
                    take(Compose(facts));
                }
            }
        }
        finally
        {
            each?.Dispose();
        }
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
        var facts = ReadFacts(symbol);
        return facts is not null ? Compose(facts) : null;
    }

    // Every touch of the library's objects describing a symbol: its properties, the library's own formatter and the
    // walk to its accessors.
    private static SymbolFacts? ReadFacts(ISymbol symbol)
    {
        switch (symbol)
        {
            case IEntity entity:
                return ReadEntityFacts(entity);
            case IModule module:
                return ReadModuleFacts(module);
            default:
                return null;
        }
    }

    private static SymbolFacts ReadModuleFacts(IModule module)
    {
        var file = module.MetadataFile is null ? null : AssemblyPath.Of(module.MetadataFile.FileName);
        return new SymbolFacts
        {
            Symbol = module, Shape = new SymbolShape.Assembly(), Accessibility = NodeAccessibility.Public, Label = module.AssemblyName,
            Container = module.MetadataFile?.FileName ?? "", AssemblyName = null,
            Reference = file is not null ? CodeReferences.CreateAssembly(module.AssemblyName, file) : null,
            Kinds = ListKinds(module), Accessors = [], File = file, Definition = null,
        };
    }

    private static SymbolFacts ReadEntityFacts(IEntity entity)
    {
        var file = entity.ParentModule?.MetadataFile;
        var path = file is null ? null : AssemblyPath.Of(file.FileName);
        var outer = entity.DeclaringTypeDefinition;
        return new SymbolFacts
        {
            Symbol = entity, Shape = EntityText.SymbolShapeOf(entity), Accessibility = EntityText.Map(entity.Accessibility), Label = EntityText.Label(entity),
            Container = outer is not null ? EntityText.FullName(outer) : entity is ITypeDefinition type ? type.Namespace : "",
            AssemblyName = entity.ParentModule?.AssemblyName, Reference = CodeReferences.Find(entity), Kinds = ListKinds(entity),
            Accessors = Members.Accessors(entity).Select(static m => ReadEntityFacts(m)).ToArray(),
            File = path, Definition = DefinitionOf(entity, path),
        };
    }

    // The app's part, on values the read took out: where the symbol is, as the analyzer shows it.
    private static AnalysisSymbol Compose(SymbolFacts facts)
    {
        var described = new AnalysisSymbol
        {
            Shape = facts.Shape, Accessibility = facts.Accessibility, Label = facts.Label, Location = Where(facts.Container, facts.AssemblyName),
            Reference = facts.Reference, Kinds = facts.Kinds, Accessors = facts.Accessors.Select(static a => Compose(a)).ToArray(),
            File = facts.File, Definition = facts.Definition,
        };
        return Keep(described, facts.Symbol);
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

    private static string Where(string container, string? assembly)
    {
        if (assembly is null)
        {
            return container;
        }

        return container.Length == 0 ? assembly : $"{container} ({assembly})";
    }

    // What describing a symbol reads of it. The container is the full name of the type declaring it, else a type's
    // namespace, else empty (a module's: its file); a module has no assembly name of its own here.
    private sealed record SymbolFacts
    {
        public required ISymbol Symbol { get; init; }
        public required SymbolShape Shape { get; init; }
        public required NodeAccessibility Accessibility { get; init; }
        public required string Label { get; init; }
        public required string Container { get; init; }
        public required string? AssemblyName { get; init; }
        public required CodeReference.Navigable? Reference { get; init; }
        public required IReadOnlyList<AnalysisKind> Kinds { get; init; }
        public required IReadOnlyList<SymbolFacts> Accessors { get; init; }
        public required AssemblyPath? File { get; init; }
        public required CodeAnchor? Definition { get; init; }
    }
}
