// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Shows the symbols an attribute is applied to.</summary>
/// <remarks>Each step reads under a catch of its own, noting a part the library fails reading as passed over: the
/// attribute type's facts, each module's attributes and what carries them, its security declarations with the type and
/// assembly of each attribute they hold, and for a built-in attribute each type's members that carry it. Which security
/// attributes are of the type, and which members are looked in for a built-in one, are decided outside.</remarks>
internal static class AttributeAppliedToAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not ITypeDefinition type)
        {
            return null;
        }

        return context => Analyze(type, context);
    }

    /// <summary>An attribute type: a type with <see cref="System.Attribute"/> among its base types.</summary>
    public static bool Applies(OfferFacts facts)
    {
        return facts.Sort == AnalyzedSort.Type && facts.IsAttribute;
    }

    private static IEnumerable<ISymbol> Analyze(ITypeDefinition attributeType, AnalyzerContext context)
    {
        var facts = ReadAnalyzed(attributeType, context);
        if (facts is null)
        {
            return [];
        }

        var scope = AnalyzerScope.Create(context, attributeType);
        if (scope is null)
        {
            return [];
        }

        var custom = facts.Known.IsCustomAttribute();
        var found = scope.ListScanned(context.CancellationToken).SelectMany(s =>
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            return custom
                ? CustomAppliedTo(attributeType, facts, s.File, s.TypeSystem, context.PassedOver)
                : BuiltinAppliedTo(facts.Known, s.File, s.TypeSystem, context.PassedOver);
        });
        return custom ? found.Distinct() : found;
    }

    // Which attribute the type is, its reflection name and its assembly's name; null when the library fails reading
    // them, noted.
    private static AnalyzedFacts? ReadAnalyzed(ITypeDefinition attributeType, AnalyzerContext context)
    {
        try
        {
            return new AnalyzedFacts
            {
                Known = attributeType.IsBuiltinAttribute(), ReflectionName = attributeType.ReflectionName, AssemblyName = attributeType.ParentModule?.AssemblyName,
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            context.PassedOver.Note(context.AnalyzedFile, e);
            return null;
        }
    }

    private sealed class AnalyzedFacts
    {
        public required KnownAttribute Known { get; init; }

        public required string ReflectionName { get; init; }

        public required string? AssemblyName { get; init; }
    }

    private static IEnumerable<ISymbol> CustomAppliedTo(ITypeDefinition attributeType, AnalyzedFacts facts, MetadataFile file, DecompilerTypeSystem ts,
        PassedOver passedOver)
    {
        var read = ReadDecoder(attributeType, file, ts, passedOver);
        if (read is null)
        {
            return [];
        }

        var (main, decoder) = read.Value;
        var owners = CustomAttributes.ListOwners(main, file, a => IsOfType(a, file, decoder, passedOver), passedOver);
        return owners.Concat(SecurityAppliedTo(facts, main, file, passedOver));
    }

    // The module's main module and the library's decoder finding the attribute type; null when the library fails making
    // it, noted.
    private static (MetadataModule Main, FindTypeDecoder Decoder)? ReadDecoder(ITypeDefinition attributeType, MetadataFile file, DecompilerTypeSystem ts,
        PassedOver passedOver)
    {
        try
        {
            var main = ts.MainModule;
            return (main, new FindTypeDecoder(main, attributeType));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    // Whether the attribute is of the type, by the library's decoder; not when it fails decoding it, noted.
    private static bool IsOfType(CustomAttribute attribute, MetadataFile file, FindTypeDecoder decoder, PassedOver passedOver)
    {
        try
        {
            return CustomAttributes.IsOfType(attribute.Constructor, file.Metadata, decoder);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return false;
        }
    }

    private static IEnumerable<ISymbol> SecurityAppliedTo(AnalyzedFacts facts, MetadataModule main, MetadataFile file, PassedOver passedOver)
    {
        var handles = ReadSecurityHandles(file, passedOver);
        foreach (var handle in handles ?? [])
        {
            var declaration = ReadSecurity(main, file, handle, passedOver);
            if (declaration is null || !declaration.Attributes.Any(a => IsOfType(a, facts)))
            {
                continue;
            }

            var owner = ReadSecurityOwner(main, file, declaration.Parent, passedOver);
            if (owner is not null)
            {
                yield return owner;
            }
        }
    }

    // ILSpy's test: an attribute of the type's reflection name, from the type's assembly when its definition is found.
    private static bool IsOfType(AttributeFacts attribute, AnalyzedFacts attributeType)
    {
        if (attribute.ReflectionName != attributeType.ReflectionName)
        {
            return false;
        }

        if (!attribute.HasDefinition)
        {
            return true;
        }

        return attribute.AssemblyName == attributeType.AssemblyName;
    }

    private static DeclarativeSecurityAttributeHandle[]? ReadSecurityHandles(MetadataFile file, PassedOver passedOver)
    {
        try
        {
            return file.Metadata.DeclarativeSecurityAttributes.ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    // What a security declaration is applied to, and the type of each attribute its holder carries, with the assembly of
    // its definition; null when the library fails reading them, noted.
    private static SecurityDeclaration? ReadSecurity(MetadataModule main, MetadataFile file, DeclarativeSecurityAttributeHandle handle, PassedOver passedOver)
    {
        try
        {
            var parent = file.Metadata.GetDeclarativeSecurityAttribute(handle).Parent;
            var attributes = SecurityAttributesOf(main, parent);
            return new SecurityDeclaration { Parent = parent, Attributes = attributes.Select(ReadAttributeFacts).ToArray() };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    private static IEnumerable<IAttribute> SecurityAttributesOf(MetadataModule main, EntityHandle parent)
    {
        if (parent.Kind == HandleKind.AssemblyDefinition)
        {
            return main.GetAssemblyAttributes();
        }

        var entity = main.ResolveEntity(parent);
        if (entity is null)
        {
            return [];
        }

        return entity.GetAttributes();
    }

    private static AttributeFacts ReadAttributeFacts(IAttribute attribute)
    {
        var definition = attribute.AttributeType.GetDefinition();
        return new AttributeFacts
        {
            ReflectionName = attribute.AttributeType.ReflectionName, HasDefinition = definition is not null, AssemblyName = definition?.ParentModule?.AssemblyName,
        };
    }

    private sealed class SecurityDeclaration
    {
        public required EntityHandle Parent { get; init; }

        public required AttributeFacts[] Attributes { get; init; }
    }

    private sealed class AttributeFacts
    {
        public required string ReflectionName { get; init; }

        public required bool HasDefinition { get; init; }

        public required string? AssemblyName { get; init; }
    }

    // What carries a security declaration: the module for an assembly's, else the entity, an accessor's property or
    // event; null when it resolves to none, or the library fails resolving it, noted.
    private static ISymbol? ReadSecurityOwner(MetadataModule main, MetadataFile file, EntityHandle parent, PassedOver passedOver)
    {
        try
        {
            if (parent.Kind == HandleKind.AssemblyDefinition)
            {
                return main;
            }

            var entity = main.ResolveEntity(parent);
            if (entity is IMethod method && method.AccessorOwner is not null)
            {
                return method.AccessorOwner;
            }

            return entity;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    // The members looked in for a built-in attribute, as ILSpy's analyzer looks: each sort a pass over the module's
    // types, a parameter's attribute twice for MarshalAs, as ILSpy lists it.
    private static IEnumerable<ISymbol> BuiltinAppliedTo(KnownAttribute attribute, MetadataFile file, DecompilerTypeSystem ts, PassedOver passedOver)
    {
        BuiltinCarrier[] carriers = attribute switch
        {
            KnownAttribute.Serializable or KnownAttribute.ComImport or KnownAttribute.StructLayout => [BuiltinCarrier.Type],
            KnownAttribute.DllImport or KnownAttribute.PreserveSig or KnownAttribute.MethodImpl => [BuiltinCarrier.Method],
            KnownAttribute.FieldOffset or KnownAttribute.NonSerialized => [BuiltinCarrier.Field],
            KnownAttribute.MarshalAs => [BuiltinCarrier.Field, BuiltinCarrier.Parameter, BuiltinCarrier.Parameter],
            KnownAttribute.Optional or KnownAttribute.In or KnownAttribute.Out => [BuiltinCarrier.Parameter],
            KnownAttribute.IndexerName => [BuiltinCarrier.Indexer],
            _ => [],
        };
        if (carriers.Length == 0)
        {
            yield break;
        }

        var types = ReadTypes(ts, file, passedOver);
        foreach (var carrier in carriers)
        {
            foreach (var type in types ?? [])
            {
                var owners = ReadBuiltinOwners(type, attribute, carrier, file, passedOver);
                foreach (var owner in owners ?? [])
                {
                    yield return owner;
                }
            }
        }
    }

    private enum BuiltinCarrier
    {
        Type,
        Method,
        Field,
        Parameter,
        Indexer,
    }

    private static ITypeDefinition[]? ReadTypes(DecompilerTypeSystem ts, MetadataFile file, PassedOver passedOver)
    {
        try
        {
            return ts.MainModule.TypeDefinitions.ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    // What of the type carries the attribute, by the library's own test: the type, its methods (an accessor's property
    // or event), its fields, its methods with a parameter carrying it, or its properties; null when the library fails
    // reading them, noted.
    private static ISymbol[]? ReadBuiltinOwners(ITypeDefinition type, KnownAttribute attribute, BuiltinCarrier carrier, MetadataFile file, PassedOver passedOver)
    {
        try
        {
            return carrier switch
            {
                BuiltinCarrier.Type => type.HasAttribute(attribute) ? [type] : [],
                BuiltinCarrier.Method => type.Members.OfType<IMethod>().Where(m => m.HasAttribute(attribute)).Select(static m => (ISymbol)(m.AccessorOwner ?? m)).ToArray(),
                BuiltinCarrier.Field => type.Fields.Where(f => f.HasAttribute(attribute)).ToArray<ISymbol>(),
                BuiltinCarrier.Parameter => type.Members.OfType<IMethod>().Where(m => m.Parameters.Any(p => p.HasAttribute(attribute)))
                    .Select(static m => (ISymbol)(m.AccessorOwner ?? m)).ToArray(),
                BuiltinCarrier.Indexer => type.Properties.Where(p => p.HasAttribute(attribute)).ToArray<ISymbol>(),
                _ => [],
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }
}
