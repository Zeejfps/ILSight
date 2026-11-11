// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Collections.Immutable;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Shows the symbols outside a type whose metadata or code uses it.</summary>
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

    private static IEnumerable<ISymbol> Analyze(ITypeDefinition analyzedType, AnalyzerContext context)
    {
        var scope = AnalyzerHelpers.GetScope(context, analyzedType);
        return AnalyzerHelpers.GetModulesInScope(scope, context.CancellationToken)
            .AsParallel().AsOrdered()
            .SelectMany(module => AnalyzeModule(analyzedType, AnalyzerHelpers.GetTypeSystem(scope, module))
                .Distinct()
                .Where(s => !analyzedType.Equals((s as IEntity)?.DeclaringTypeDefinition)));
    }

    private static IEnumerable<ISymbol> AnalyzeModule(ITypeDefinition analyzedType, DecompilerTypeSystem typeSystem)
    {
        var module = typeSystem.MainModule;
        var metadata = module.MetadataFile.Metadata;
        var decoder = new FindTypeDecoder(module, analyzedType);
        var attributeDecoder = AnalyzerHelpers.CreateDecoder(module, analyzedType);

        foreach (var result in AnalyzerHelpers.AttributeOwners(typeSystem, UsesType))
        {
            yield return result;
        }

        foreach (var h in metadata.TypeDefinitions)
        {
            var td = metadata.GetTypeDefinition(h);
            var found = decoder.GetTypeFromEntity(metadata, td.GetBaseTypeOrNil());
            foreach (var ih in td.GetInterfaceImplementations())
            {
                found |= decoder.GetTypeFromEntity(metadata, metadata.GetInterfaceImplementation(ih).Interface);
            }

            found |= IsUsedInGenericConstraints(metadata, td.GetGenericParameters(), decoder);
            if (found)
            {
                yield return module.GetDefinition(h);
            }
        }

        foreach (var h in metadata.MethodDefinitions)
        {
            var md = metadata.GetMethodDefinition(h);
            var found = FindTypeDecoder.AnyInMethodSignature(md.DecodeSignature(decoder, default));
            found |= IsUsedInGenericConstraints(metadata, md.GetGenericParameters(), decoder);
            if (found || IsUsedInMethodBody(module.MetadataFile, h, decoder))
            {
                var method = module.GetDefinition(h);
                yield return method.AccessorOwner ?? method;
            }
        }

        foreach (var h in metadata.FieldDefinitions)
        {
            var field = metadata.GetFieldDefinition(h);
            if (field.DecodeSignature(decoder, default))
            {
                yield return module.GetDefinition(h);
            }
        }

        foreach (var h in metadata.PropertyDefinitions)
        {
            var signature = metadata.GetPropertyDefinition(h).DecodeSignature(decoder, default);
            if (FindTypeDecoder.AnyInMethodSignature(signature))
            {
                yield return module.GetDefinition(h);
            }
        }

        foreach (var h in metadata.EventDefinitions)
        {
            var eventType = metadata.GetEventDefinition(h).Type;
            if (decoder.GetTypeFromEntity(metadata, eventType))
            {
                yield return module.GetDefinition(h);
            }
        }

        bool UsesType(CustomAttribute attribute)
        {
            CustomAttributeValue<TokenSearchResult> value;
            try
            {
                value = attribute.DecodeValue(attributeDecoder);
            }
            catch (EnumUnderlyingTypeResolveException)
            {
                return false;
            }
            return AnalyzerHelpers.IsCustomAttributeOfType(attribute.Constructor, metadata, decoder)
                || value.FixedArguments.Any(a => IsFound(a.Value)) || value.NamedArguments.Any(a => IsFound(a.Value));
        }
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

    private static bool IsUsedInGenericConstraints(MetadataReader metadata, GenericParameterHandleCollection parameters, FindTypeDecoder decoder)
    {
        return parameters.Any(h => metadata.GetGenericParameter(h).GetConstraints()
            .Any(c => decoder.GetTypeFromEntity(metadata, metadata.GetGenericParameterConstraint(c).Type)));
    }

    private static bool IsUsedInMethodBody(MetadataFile module, MethodDefinitionHandle handle, FindTypeDecoder decoder)
    {
        var body = AnalyzerHelpers.ReadBody(module, handle);
        if (body is null)
        {
            return false;
        }

        var metadata = module.Metadata;
        if (!body.LocalSignature.IsNil)
        {
            try
            {
                var localSignature = metadata.GetStandaloneSignature(body.LocalSignature);
                if (IsUsedBySignature(localSignature))
                {
                    return true;
                }
            }
            catch (BadImageFormatException)
            {
            }
        }
        return AnalyzerHelpers.HasToken(body, (_, token) => IsUsedByMember(token));

        bool IsUsedByMember(EntityHandle member)
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
                    return IsUsedByMember(fd.GetDeclaringType()) || fd.DecodeSignature(decoder, default);
                case HandleKind.MethodDefinition:
                    var md = metadata.GetMethodDefinition((MethodDefinitionHandle)member);
                    return IsUsedByMember(md.GetDeclaringType()) || FindTypeDecoder.AnyInMethodSignature(md.DecodeSignature(decoder, default));
                case HandleKind.MemberReference:
                    var mr = metadata.GetMemberReference((MemberReferenceHandle)member);
                    return IsUsedByMember(mr.Parent) || mr.GetKind() switch
                    {
                        MemberReferenceKind.Method => FindTypeDecoder.AnyInMethodSignature(mr.DecodeMethodSignature(decoder, default)),
                        MemberReferenceKind.Field => mr.DecodeFieldSignature(decoder, default),
                        _ => false,
                    };
                case HandleKind.MethodSpecification:
                    var ms = metadata.GetMethodSpecification((MethodSpecificationHandle)member);
                    return IsUsedByMember(ms.Method) || ms.DecodeSignature(decoder, default).Any(static t => t);
                case HandleKind.StandaloneSignature:
                    return IsUsedBySignature(metadata.GetStandaloneSignature((StandaloneSignatureHandle)member));
                default:
                    return false;
            }
        }

        bool IsUsedBySignature(StandaloneSignature signature)
        {
            return signature.GetKind() switch
            {
                StandaloneSignatureKind.Method => FindTypeDecoder.AnyInMethodSignature(signature.DecodeMethodSignature(decoder, default)),
                StandaloneSignatureKind.LocalVariables => signature.DecodeLocalSignature(decoder, default).Any(static t => t),
                _ => false,
            };
        }
    }
}
