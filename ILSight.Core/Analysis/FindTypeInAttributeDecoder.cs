// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Finds a type in the attribute values of a module.</summary>
internal sealed class FindTypeInAttributeDecoder : ICustomAttributeTypeProvider<TokenSearchResult>
{
    public required MetadataFile DeclaringModule { get; init; }
    public required MetadataModule CurrentModule { get; init; }
    public required TypeDefinitionHandle Handle { get; init; }
    public required PrimitiveTypeCode PrimitiveType { get; init; }

    /// <summary>A decoder finding <paramref name="type"/> in the attribute values of <paramref name="currentModule"/>.</summary>
    public static FindTypeInAttributeDecoder Create(MetadataModule currentModule, ITypeDefinition type)
    {
        return new FindTypeInAttributeDecoder
        {
            CurrentModule = currentModule,
            DeclaringModule = type.ParentModule?.MetadataFile ?? throw new InvalidOperationException("Cannot use MetadataModule without PEFile as context."),
            Handle = DecompilerFacts.GetTypeHandle(type),
            PrimitiveType = type.KnownTypeCode == KnownTypeCode.None ? 0 : type.KnownTypeCode.ToPrimitiveTypeCode(),
        };
    }

    public TokenSearchResult GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return typeCode == PrimitiveType ? TokenSearchResult.Found : 0;
    }

    public TokenSearchResult GetSystemType()
    {
        return TokenSearchResult.SystemType;
    }

    public TokenSearchResult GetSZArrayType(TokenSearchResult elementType)
    {
        return elementType & TokenSearchResult.Found;
    }

    public TokenSearchResult GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var result = TokenSearchResult.NoResult;
        if (handle.IsEnum(reader, out var underlyingType))
        {
            result = (TokenSearchResult)underlyingType;
        }
        else if (((EntityHandle)handle).IsKnownType(reader, KnownTypeCode.Type))
        {
            result = TokenSearchResult.SystemType;
        }

        if (Handle == handle && reader == DeclaringModule.Metadata)
        {
            result |= TokenSearchResult.Found;
        }

        return result;
    }

    public TokenSearchResult GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        return GetResultFromResolvedType(CurrentModule.ResolveType(handle, default));
    }

    public TokenSearchResult GetTypeFromSerializedName(string name)
    {
        if (name is null)
        {
            return TokenSearchResult.NoResult;
        }

        try
        {
            return GetResultFromResolvedType(ReflectionHelper.ParseReflectionName(name, new SimpleTypeResolveContext(CurrentModule)));
        }
        catch (ReflectionNameParseException)
        {
            return TokenSearchResult.NoResult;
        }
    }

    public PrimitiveTypeCode GetUnderlyingEnumType(TokenSearchResult type)
    {
        var typeCode = type & TokenSearchResult.TypeCodeMask;
        if (typeCode == 0 || typeCode == TokenSearchResult.SystemType)
        {
            throw new EnumUnderlyingTypeResolveException();
        }

        return (PrimitiveTypeCode)typeCode;
    }

    public bool IsSystemType(TokenSearchResult type)
    {
        return (type & TokenSearchResult.TypeCodeMask) == TokenSearchResult.SystemType;
    }

    private TokenSearchResult GetResultFromResolvedType(IType type)
    {
        var definition = type.GetDefinition();
        if (definition is null)
        {
            return TokenSearchResult.NoResult;
        }

        var result = TokenSearchResult.NoResult;
        var underlyingType = definition.EnumUnderlyingType?.GetDefinition();
        if (underlyingType is not null)
        {
            result = (TokenSearchResult)underlyingType.KnownTypeCode.ToPrimitiveTypeCode();
        }
        else if (definition.KnownTypeCode == KnownTypeCode.Type)
        {
            result = TokenSearchResult.SystemType;
        }

        if (definition.MetadataToken == Handle && definition.ParentModule?.MetadataFile == DeclaringModule)
        {
            result |= TokenSearchResult.Found;
        }

        return result;
    }
}
