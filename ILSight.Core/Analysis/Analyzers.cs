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
        new AnalysisKind
        {
            Name = AnalysisName.AppliedTo, Order = 10, Prepare = AttributeAppliedToAnalyzer.Prepare, Applies = AttributeAppliedToAnalyzer.Applies,
        },
        new AnalysisKind
        {
            Name = AnalysisName.ImplementedBy, Order = 10, Prepare = static s => InheritorsAnalyzer<IEvent>.Prepare(overrides: false, s),
            Applies = static f => InheritorsAnalyzer<IEvent>.Applies(overrides: false, AnalyzedSort.Event, f),
        },
        new AnalysisKind
        {
            Name = AnalysisName.OverriddenBy, Order = 20, Prepare = static s => InheritorsAnalyzer<IEvent>.Prepare(overrides: true, s),
            Applies = static f => InheritorsAnalyzer<IEvent>.Applies(overrides: true, AnalyzedSort.Event, f),
        },
        new AnalysisKind
        {
            Name = AnalysisName.ReadBy, Order = 10, Prepare = static s => FieldAccessAnalyzer.Prepare(FieldAccessKind.Read, s),
            Applies = static f => FieldAccessAnalyzer.Applies(FieldAccessKind.Read, f),
        },
        new AnalysisKind
        {
            Name = AnalysisName.AssignedBy, Order = 20, Prepare = static s => FieldAccessAnalyzer.Prepare(FieldAccessKind.Write, s),
            Applies = static f => FieldAccessAnalyzer.Applies(FieldAccessKind.Write, f),
        },
        new AnalysisKind
        {
            Name = AnalysisName.AddressTakenBy, Order = 30, Prepare = static s => FieldAccessAnalyzer.Prepare(FieldAccessKind.AddressOf, s),
            Applies = static f => FieldAccessAnalyzer.Applies(FieldAccessKind.AddressOf, f),
        },
        new AnalysisKind
        {
            Name = AnalysisName.Implements, Order = 40, Prepare = MemberImplementsInterfaceAnalyzer.Prepare,
            Applies = MemberImplementsInterfaceAnalyzer.Applies,
        },
        new AnalysisKind { Name = AnalysisName.Overrides, Order = 35, Prepare = MemberOverridesAnalyzer.Prepare, Applies = MemberOverridesAnalyzer.Applies },
        new AnalysisKind
        {
            Name = AnalysisName.ImplementedBy, Order = 40, Prepare = static s => InheritorsAnalyzer<IMethod>.Prepare(overrides: false, s),
            Applies = static f => InheritorsAnalyzer<IMethod>.Applies(overrides: false, AnalyzedSort.Method, f),
        },
        new AnalysisKind
        {
            Name = AnalysisName.OverriddenBy, Order = 30, Prepare = static s => InheritorsAnalyzer<IMethod>.Prepare(overrides: true, s),
            Applies = static f => InheritorsAnalyzer<IMethod>.Applies(overrides: true, AnalyzedSort.Method, f),
        },
        new AnalysisKind { Name = AnalysisName.UsedBy, Order = 20, Prepare = MethodUsedByAnalyzer.Prepare, Applies = MethodUsedByAnalyzer.Applies },
        new AnalysisKind { Name = AnalysisName.Uses, Order = 10, Prepare = MethodUsesAnalyzer.Prepare, Applies = MethodUsesAnalyzer.Applies },
        new AnalysisKind { Name = AnalysisName.UsedBy, Order = 5, Prepare = MemberUsedByAnalyzer.Prepare, Applies = MemberUsedByAnalyzer.Applies },
        new AnalysisKind
        {
            Name = AnalysisName.ImplementedBy, Order = 10, Prepare = static s => InheritorsAnalyzer<IProperty>.Prepare(overrides: false, s),
            Applies = static f => InheritorsAnalyzer<IProperty>.Applies(overrides: false, AnalyzedSort.Property, f),
        },
        new AnalysisKind
        {
            Name = AnalysisName.OverriddenBy, Order = 20, Prepare = static s => InheritorsAnalyzer<IProperty>.Prepare(overrides: true, s),
            Applies = static f => InheritorsAnalyzer<IProperty>.Applies(overrides: true, AnalyzedSort.Property, f),
        },
        new AnalysisKind { Name = AnalysisName.ExposedBy, Order = 40, Prepare = TypeExposedByAnalyzer.Prepare, Applies = TypeExposedByAnalyzer.Applies },
        new AnalysisKind
        {
            Name = AnalysisName.ExtensionMethods, Order = 50, Prepare = TypeExtensionMethodsAnalyzer.Prepare, Applies = TypeExtensionMethodsAnalyzer.Applies,
        },
        new AnalysisKind
        {
            Name = AnalysisName.InstantiatedBy, Order = 20, Prepare = TypeInstantiatedByAnalyzer.Prepare, Applies = TypeInstantiatedByAnalyzer.Applies,
        },
        new AnalysisKind
        {
            Name = AnalysisName.UsedBy, Order = 30, Prepare = TypeUsedByAnalyzer.Prepare, Applies = TypeUsedByAnalyzer.Applies, SortsResults = true,
        },
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

    /// <summary>The facts of the type or member a node of <paramref name="subject"/> stands for — for a base or derived
    /// type entry the type it names — or null for any other node: only what the read takes out of the library, so that
    /// a node made inside its parent's listing never runs the app's table there. The analyses offered are composed from
    /// them when first asked for (<see cref="AnalysisFacts.Symbol"/>).</summary>
    internal static AnalysisFacts? Of(NodeSubject subject)
    {
        return subject.Match<AnalysisFacts?>(
            module: static _ => null,
            @namespace: static _ => null,
            failedAssembly: static _ => null,
            typeDefinition: static type => Read(NodeDefinitions.GetSymbol(type.Definition)),
            memberDefinition: static member => Read(NodeDefinitions.GetSymbol(member.Definition)),
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
            derivedType: static derived => Read(NodeDefinitions.GetSymbol(derived.Value)),
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

    private static AnalysisFacts? DescribeBase(BaseTypeInfo baseType)
    {
        var definition = NodeDefinitions.GetSymbols(baseType).Definition;
        if (definition is null)
        {
            return null;
        }

        return Read(definition);
    }

    private static AnalysisFacts? Read(ISymbol symbol)
    {
        var facts = ReadFacts(symbol);
        return facts is not null ? new AnalysisFacts(facts) : null;
    }

    /// <summary>The symbol <paramref name="facts"/> were read from, described with the analyses the app's table offers
    /// for it: the app's part, run outside any read's catch, so that a bug in the table fails whatever asked for it
    /// at its edge.</summary>
    internal static AnalysisSymbol Compose(AnalysisFacts facts)
    {
        return Compose(facts.Facts, Kinds);
    }

    /// <summary>As <see cref="Compose(AnalysisFacts)"/>, the analyses offered taken from <paramref name="table"/>.</summary>
    internal static AnalysisSymbol Compose(AnalysisFacts facts, IReadOnlyList<AnalysisKind> table)
    {
        return Compose(facts.Facts, table);
    }

    /// <summary>The type or member <paramref name="definition"/> names, loading its assembly's file
    /// without listing it, once the read of its type system ended; not found when the file is gone or its module has
    /// no such row, unreadable when the library could not read the module; a file there that can't be read, and what
    /// else the library throws resolving or describing it, <see cref="Outcome{T}.Failed"/>.</summary>
    public static Task<Outcome<Lookup<AnalysisSymbol>>> FindAsync(AssemblyList list, CodeAnchor definition, CancellationToken ct)
    {
        return FindAsync(list, definition, Kinds, ct);
    }

    /// <summary>As <see cref="FindAsync(AssemblyList, CodeAnchor, CancellationToken)"/>, the analyses offered for the
    /// symbol taken from <paramref name="table"/>.</summary>
    internal static async Task<Outcome<Lookup<AnalysisSymbol>>> FindAsync(AssemblyList list, CodeAnchor definition, IReadOnlyList<AnalysisKind> table,
        CancellationToken ct)
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
            r => Find(assembly, r.TypeSystem.MainModule, definition, table, ct),
            u => new Outcome<Lookup<AnalysisSymbol>>.Ok(Lookup.FromUnreadable<AnalysisSymbol>(assembly, u)),
            static _ => new Outcome<Lookup<AnalysisSymbol>>.Ok(new Lookup<AnalysisSymbol>.NotFound()));
    }

    // Whether the saved token still names a row is asked of the module first; what the library throws resolving one
    // that does is a failure: the module's own as unreadable, the rest with why. The analyses offered are the app's
    // decision, made after the catch.
    private static Outcome<Lookup<AnalysisSymbol>> Find(LoadedModule assembly, MetadataModule main, CodeAnchor definition,
        IReadOnlyList<AnalysisKind> table, CancellationToken ct)
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

        return new Outcome<Lookup<AnalysisSymbol>>.Ok(Lookup.FromNullable(facts is not null ? Compose(facts, table) : null));
    }

    // The app's table, applied on facts read out: the analyses that apply, in their order.
    private static IReadOnlyList<AnalysisKind> ListKinds(OfferFacts facts, IReadOnlyList<AnalysisKind> table)
    {
        return table.Where(k => k.Applies(facts)).OrderBy(static k => k.Order).ToArray();
    }

    /// <summary>Runs <paramref name="kind"/> on <paramref name="symbol"/> over the open assemblies on
    /// the thread pool, once the reads of the type systems of the modules it can reach ended, yielding results in
    /// batches as they are found, and after them each module it reached that the library could not read, then each it
    /// read only in part. Results come
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

            var run = symbol.Kinds.Contains(kind) ? kind.Prepare(entity) : null;
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

            foreach (var (name, count, why) in context.PassedOver.List())
            {
                emit(new AnalysisFind.PartlyRead { Name = name, Count = count, Why = why });
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
                    take(Compose(facts, Kinds));
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
            Offers = OfferFacts.OfModule, Accessors = [], File = file, Definition = null,
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
            AssemblyName = entity.ParentModule?.AssemblyName, Reference = CodeReferences.Find(entity), Offers = ReadOfferFacts(entity),
            Accessors = Members.Accessors(entity).Select(static m => ReadEntityFacts(m)).ToArray(),
            File = path, Definition = DefinitionOf(entity, path),
        };
    }

    // What the app's table of analyses decides on, read with the rest: the library's flags and kinds, its walk to the
    // accessors and to the base types.
    private static OfferFacts ReadOfferFacts(IEntity entity)
    {
        var member = entity as IMember;
        var method = entity as IMethod;
        return new OfferFacts
        {
            Sort = SortOf(entity), SymbolKind = entity.SymbolKind, IsStatic = entity.IsStatic, IsAbstract = entity.IsAbstract,
            IsOverride = member?.IsOverride ?? false, IsOverridable = member?.IsOverridable ?? false, IsVirtual = method?.IsVirtual ?? false,
            IsConst = entity is IField { IsConst: true }, HasBody = method?.HasBody ?? false, HasParentModule = entity.ParentModule is not null,
            DeclaringTypeKind = member?.DeclaringType.Kind, DeclaringDefinitionKind = entity.DeclaringTypeDefinition?.Kind,
            HasAccessors = member is IProperty or IEvent && Members.Accessors(member).Any(),
            IsAttribute = entity is ITypeDefinition type && type.GetNonInterfaceBaseTypes().Any(static t => t.IsKnownType(KnownTypeCode.Attribute)),
        };
    }

    private static AnalyzedSort SortOf(IEntity entity)
    {
        return entity switch
        {
            ITypeDefinition => AnalyzedSort.Type,
            IField => AnalyzedSort.Field,
            IMethod => AnalyzedSort.Method,
            IProperty => AnalyzedSort.Property,
            IEvent => AnalyzedSort.Event,
            _ => AnalyzedSort.Other,
        };
    }

    // The app's part, on values the read took out: where the symbol is, as the analyzer shows it, and the analyses
    // the table offers for it.
    private static AnalysisSymbol Compose(SymbolFacts facts, IReadOnlyList<AnalysisKind> table)
    {
        var described = new AnalysisSymbol
        {
            Shape = facts.Shape, Accessibility = facts.Accessibility, Label = facts.Label, Location = Where(facts.Container, facts.AssemblyName),
            Reference = facts.Reference, Kinds = ListKinds(facts.Offers, table), Accessors = facts.Accessors.Select(a => Compose(a, table)).ToArray(),
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
}
