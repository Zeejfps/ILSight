// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Collections.Immutable;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Shows the symbols outside a type whose metadata or code uses it.</summary>
/// <remarks>Each step reads under a catch of its own, noting a part the library fails reading as passed over: the
/// analyzed type's facts, each module's decoders, each attribute's value, each definition's use of the type by the
/// library's decoder (its signature, bases and constraints), each body's locals and instructions, then each symbol
/// found with the type declaring it. Whether an attribute's value names the type, and which symbols are the type's own,
/// are decided outside. The attribute decoder's own work runs in a <see cref="CallbackFailure"/> of the module's, which
/// the catch around the decode lets through.</remarks>
internal static class TypeUsedByAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not ITypeDefinition type)
        {
            return null;
        }

        return context => Analyze(type, context);
    }

    /// <summary>Any type.</summary>
    public static bool Applies(OfferFacts facts)
    {
        return facts.Sort == AnalyzedSort.Type;
    }

    private static IEnumerable<ISymbol> Analyze(ITypeDefinition analyzedType, AnalyzerContext context)
    {
        var facts = ReadAnalyzed(analyzedType, context);
        if (facts is null)
        {
            return [];
        }

        var scope = AnalyzerScope.Create(context, analyzedType);
        if (scope is null)
        {
            return [];
        }

        var passedOver = context.PassedOver;
        return scope.ListScanned(context.CancellationToken)
            .AsParallel().AsOrdered()
            .SelectMany(s => AnalyzeModule(analyzedType, facts, s.File, s.TypeSystem, passedOver)
                .Distinct()
                .Where(f => f.Declaring is null || !f.Declaring.Value.Is(facts.Key))
                .Select(static f => f.Symbol));
    }

    // The analyzed type's definition, and what the attribute decoder compares with: its row, its file and metadata,
    // and the primitive type it is; null when the library fails reading them, noted.
    private static AnalyzedFacts? ReadAnalyzed(ITypeDefinition type, AnalyzerContext context)
    {
        try
        {
            var file = type.ParentModule?.MetadataFile ?? throw new InvalidOperationException("Cannot use MetadataModule without PEFile as context.");
            return new AnalyzedFacts
            {
                Key = DefinitionKey.Of(type), File = file, Metadata = file.Metadata, Handle = DecompilerFacts.GetTypeHandle(type),
                PrimitiveType = type.KnownTypeCode == KnownTypeCode.None ? 0 : type.KnownTypeCode.ToPrimitiveTypeCode(),
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
        public required DefinitionKey Key { get; init; }
        public required MetadataFile File { get; init; }
        public required MetadataReader Metadata { get; init; }
        public required TypeDefinitionHandle Handle { get; init; }
        public required PrimitiveTypeCode PrimitiveType { get; init; }
    }

    // A symbol found, with the definition of the type declaring it; none for a module or a type at the top.
    private readonly record struct FoundSymbol
    {
        public required ISymbol Symbol { get; init; }

        public required DefinitionKey? Declaring { get; init; }
    }

    private static IEnumerable<FoundSymbol> AnalyzeModule(ITypeDefinition analyzedType, AnalyzedFacts analyzed, MetadataFile file, DecompilerTypeSystem typeSystem,
        PassedOver passedOver)
    {
        var setup = ReadModule(analyzedType, file, typeSystem, passedOver);
        if (setup is null)
        {
            yield break;
        }

        var (module, metadata, decoder) = setup.Value;
        var callbacks = new CallbackFailure();
        var attributeDecoder = new FindTypeInAttributeDecoder
        {
            PassedOver = passedOver, Callbacks = callbacks, CurrentModule = module, CurrentFile = file, DeclaringModule = analyzed.File,
            DeclaringMetadata = analyzed.Metadata, Handle = analyzed.Handle, PrimitiveType = analyzed.PrimitiveType,
        };

        var owners = CustomAttributes.ListOwners(module, file, a => UsesType(a, file, decoder, attributeDecoder, callbacks, passedOver), passedOver);
        foreach (var owner in owners)
        {
            var found = ReadFound(owner, file, passedOver);
            if (found is not null)
            {
                yield return found.Value;
            }
        }

        foreach (var row in metadata.TypeDefinitions)
        {
            var uses = ReadTypeUse(file, row, decoder, passedOver);
            if (uses != true)
            {
                continue;
            }

            var found = ReadDefinition(module, file, row, passedOver);
            if (found is not null)
            {
                yield return found.Value;
            }
        }

        foreach (var handle in metadata.MethodDefinitions)
        {
            var uses = ReadMethodSignatureUse(file, handle, decoder, passedOver);
            if (uses is null || uses == false && !IsUsedInMethodBody(file, handle, decoder, passedOver))
            {
                continue;
            }

            var found = ReadMethodOwner(module, file, handle, passedOver);
            if (found is not null)
            {
                yield return found.Value;
            }
        }

        foreach (var row in metadata.FieldDefinitions)
        {
            var uses = ReadFieldUse(file, row, decoder, passedOver);
            if (uses != true)
            {
                continue;
            }

            var found = ReadDefinition(module, file, row, passedOver);
            if (found is not null)
            {
                yield return found.Value;
            }
        }

        foreach (var row in metadata.PropertyDefinitions)
        {
            var uses = ReadPropertyUse(file, row, decoder, passedOver);
            if (uses != true)
            {
                continue;
            }

            var found = ReadDefinition(module, file, row, passedOver);
            if (found is not null)
            {
                yield return found.Value;
            }
        }

        foreach (var row in metadata.EventDefinitions)
        {
            var uses = ReadEventUse(file, row, decoder, passedOver);
            if (uses != true)
            {
                continue;
            }

            var found = ReadDefinition(module, file, row, passedOver);
            if (found is not null)
            {
                yield return found.Value;
            }
        }
    }

    // The module's main module, its metadata and the library's decoder finding the type; null when the library fails
    // making them, noted.
    private static (MetadataModule Module, MetadataReader Metadata, FindTypeDecoder Decoder)? ReadModule(ITypeDefinition analyzedType, MetadataFile file, DecompilerTypeSystem typeSystem,
        PassedOver passedOver)
    {
        try
        {
            var module = typeSystem.MainModule;
            return (module, file.Metadata, new FindTypeDecoder(module, analyzedType));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    // Whether the attribute names the type, by its constructor's type or a value of one of its arguments; not when its
    // value can't be decoded, which is noted. The decoder's own work is counted: a bug in it is let through.
    private static bool UsesType(CustomAttribute attribute, MetadataFile file, FindTypeDecoder decoder, FindTypeInAttributeDecoder attributeDecoder,
        CallbackFailure callbacks, PassedOver passedOver)
    {
        CustomAttributeValue<TokenSearchResult> value;
        bool ofType;
        try
        {
            value = attribute.DecodeValue(attributeDecoder);
            ofType = CustomAttributes.IsOfType(attribute.Constructor, file.Metadata, decoder);
        }
        catch (Exception e) when (e is not OperationCanceledException && !callbacks.Failed)
        {
            // The value can't be decoded: what type it names is not known.
            passedOver.Note(file, e);
            return false;
        }

        return ofType || value.FixedArguments.Any(a => IsFound(a.Value)) || value.NamedArguments.Any(a => IsFound(a.Value));
    }

    private static bool IsFound(object? value)
    {
        return value switch
        {
            TokenSearchResult result => (result & TokenSearchResult.Found) != 0,
            ImmutableArray<CustomAttributeTypedArgument<IType>> array => array.Any(element => IsFound(element.Value)),
            _ => false,
        };
    }

    // Whether a type's base type, an interface it implements or a constraint of its generic parameters names the type;
    // null when the library fails decoding them, noted.
    private static bool? ReadTypeUse(MetadataFile file, TypeDefinitionHandle handle, FindTypeDecoder decoder, PassedOver passedOver)
    {
        try
        {
            var metadata = file.Metadata;
            var td = metadata.GetTypeDefinition(handle);
            var found = decoder.GetTypeFromEntity(metadata, td.GetBaseTypeOrNil());
            foreach (var ih in td.GetInterfaceImplementations())
            {
                found |= decoder.GetTypeFromEntity(metadata, metadata.GetInterfaceImplementation(ih).Interface);
            }

            return found | IsUsedInGenericConstraints(metadata, td.GetGenericParameters(), decoder);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    // Whether a method's signature or a constraint of its generic parameters names the type; null when the library
    // fails decoding them, noted.
    private static bool? ReadMethodSignatureUse(MetadataFile file, MethodDefinitionHandle handle, FindTypeDecoder decoder, PassedOver passedOver)
    {
        try
        {
            var metadata = file.Metadata;
            var md = metadata.GetMethodDefinition(handle);
            var found = FindTypeDecoder.AnyInMethodSignature(md.DecodeSignature(decoder, default));
            return found | IsUsedInGenericConstraints(metadata, md.GetGenericParameters(), decoder);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    private static bool? ReadFieldUse(MetadataFile file, FieldDefinitionHandle handle, FindTypeDecoder decoder, PassedOver passedOver)
    {
        try
        {
            return file.Metadata.GetFieldDefinition(handle).DecodeSignature(decoder, default);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    private static bool? ReadPropertyUse(MetadataFile file, PropertyDefinitionHandle handle, FindTypeDecoder decoder, PassedOver passedOver)
    {
        try
        {
            var signature = file.Metadata.GetPropertyDefinition(handle).DecodeSignature(decoder, default);
            return FindTypeDecoder.AnyInMethodSignature(signature);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    private static bool? ReadEventUse(MetadataFile file, EventDefinitionHandle handle, FindTypeDecoder decoder, PassedOver passedOver)
    {
        try
        {
            var metadata = file.Metadata;
            return decoder.GetTypeFromEntity(metadata, metadata.GetEventDefinition(handle).Type);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    private static bool IsUsedInGenericConstraints(MetadataReader metadata, GenericParameterHandleCollection parameters, FindTypeDecoder decoder)
    {
        return parameters.Any(h => metadata.GetGenericParameter(h).GetConstraints()
            .Any(c => decoder.GetTypeFromEntity(metadata, metadata.GetGenericParameterConstraint(c).Type)));
    }

    // The definition the row names, with the type declaring it; null when the library fails reading it, noted.
    private static FoundSymbol? ReadDefinition(MetadataModule module, MetadataFile file, EntityHandle row, PassedOver passedOver)
    {
        try
        {
            var entity = module.ResolveEntity(row);
            return Describe(entity);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    // The method, or the property or event it is an accessor of, with the type declaring it; null when the library fails
    // reading it, noted.
    private static FoundSymbol? ReadMethodOwner(MetadataModule module, MetadataFile file, MethodDefinitionHandle handle, PassedOver passedOver)
    {
        try
        {
            var method = module.GetDefinition(handle);
            return Describe(method.AccessorOwner ?? method);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    // A symbol found another way (what carries an attribute), with the type declaring it; null when the library fails
    // reading it, noted.
    private static FoundSymbol? ReadFound(ISymbol symbol, MetadataFile file, PassedOver passedOver)
    {
        try
        {
            return Describe(symbol);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    private static FoundSymbol Describe(ISymbol symbol)
    {
        var declaring = (symbol as IEntity)?.DeclaringTypeDefinition;
        return new FoundSymbol { Symbol = symbol, Declaring = declaring is null ? null : DefinitionKey.Of(declaring) };
    }

    private static bool IsUsedInMethodBody(MetadataFile module, MethodDefinitionHandle handle, FindTypeDecoder decoder, PassedOver passedOver)
    {
        var body = MethodBodies.Read(module, handle, passedOver);
        if (body is null)
        {
            return false;
        }

        var localsUse = ReadLocalsUse(module, body, decoder, passedOver);
        if (localsUse == true)
        {
            return true;
        }

        foreach (var (_, token) in MethodBodies.ListTokens(module, body, passedOver))
        {
            var uses = ReadMemberUse(module, token, decoder, passedOver);
            if (uses == true)
            {
                return true;
            }
        }
        return false;
    }

    // Whether the body's locals name the type; null when it has none, or they cannot be read, which is noted: the code
    // may still show a use.
    private static bool? ReadLocalsUse(MetadataFile module, MethodBodyBlock body, FindTypeDecoder decoder, PassedOver passedOver)
    {
        try
        {
            var locals = body.LocalSignature;
            return locals.IsNil ? null : IsUsedBySignature(module.Metadata, module.Metadata.GetStandaloneSignature(locals), decoder);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(module, e);
            return null;
        }
    }

    // Whether what the token names uses the type, by the library's decoder; null when it can't decode it, noted.
    private static bool? ReadMemberUse(MetadataFile module, EntityHandle token, FindTypeDecoder decoder, PassedOver passedOver)
    {
        try
        {
            return IsUsedByMember(module.Metadata, token, decoder);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(module, e);
            return null;
        }
    }

    private static bool IsUsedByMember(MetadataReader metadata, EntityHandle member, FindTypeDecoder decoder)
    {
        if (member.IsNil)
        {
            return false;
        }

        switch (member.Kind)
        {
            case HandleKind.TypeReference:
                return decoder.GetTypeFromReference(metadata, (TypeReferenceHandle)member, 0);
            case HandleKind.TypeSpecification:
                return decoder.GetTypeFromSpecification(metadata, default, (TypeSpecificationHandle)member, 0);
            case HandleKind.TypeDefinition:
                return decoder.GetTypeFromDefinition(metadata, (TypeDefinitionHandle)member, 0);
            case HandleKind.FieldDefinition:
                var fd = metadata.GetFieldDefinition((FieldDefinitionHandle)member);
                return IsUsedByMember(metadata, fd.GetDeclaringType(), decoder) || fd.DecodeSignature(decoder, default);
            case HandleKind.MethodDefinition:
                var md = metadata.GetMethodDefinition((MethodDefinitionHandle)member);
                return IsUsedByMember(metadata, md.GetDeclaringType(), decoder) || FindTypeDecoder.AnyInMethodSignature(md.DecodeSignature(decoder, default));
            case HandleKind.MemberReference:
                var mr = metadata.GetMemberReference((MemberReferenceHandle)member);
                return IsUsedByMember(metadata, mr.Parent, decoder) || mr.GetKind() switch
                {
                    MemberReferenceKind.Method => FindTypeDecoder.AnyInMethodSignature(mr.DecodeMethodSignature(decoder, default)),
                    MemberReferenceKind.Field => mr.DecodeFieldSignature(decoder, default),
                    _ => false,
                };
            case HandleKind.MethodSpecification:
                var ms = metadata.GetMethodSpecification((MethodSpecificationHandle)member);
                return IsUsedByMember(metadata, ms.Method, decoder) || ms.DecodeSignature(decoder, default).Any(static t => t);
            case HandleKind.StandaloneSignature:
                return IsUsedBySignature(metadata, metadata.GetStandaloneSignature((StandaloneSignatureHandle)member), decoder);
            default:
                return false;
        }
    }

    private static bool IsUsedBySignature(MetadataReader metadata, StandaloneSignature signature, FindTypeDecoder decoder)
    {
        return signature.GetKind() switch
        {
            StandaloneSignatureKind.Method => FindTypeDecoder.AnyInMethodSignature(signature.DecodeMethodSignature(decoder, default)),
            StandaloneSignatureKind.LocalVariables => signature.DecodeLocalSignature(decoder, default).Any(static t => t),
            _ => false,
        };
    }
}
